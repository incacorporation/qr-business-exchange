#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Sets the API's CardPointe (Fiserv CardConnect) settings on the IIS server, as environment
    variables of the API site's application pool.

.DESCRIPTION
    Run this ON THE WEB SERVER (the one behind webapi.qrbound.com), in an elevated PowerShell.
    It asks for each value; secrets are typed without being shown and are not written to the
    screen, the repository or any file other than IIS's own configuration (applicationHost.config).

    App pool environment variables survive deployments: publishing replaces web.config and
    appsettings*.json, but not these. The API reads them as CardPointe__<Setting>.

    Press Enter at a prompt to keep the value that's already set (shown as [set]) or to leave an
    optional value empty. The app pool is recycled at the end so the API picks the values up.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Set-CardPointeSettings.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Set-CardPointeSettings.ps1 -Site webapi.qrbound.com
#>
param(
    [string] $Site = 'webapi.qrbound.com'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$pool = (Get-Item "IIS:\Sites\$Site").applicationPool
if (-not $pool) { throw "IIS site '$Site' not found, or it has no application pool." }
Write-Host "Site '$Site' runs in app pool '$pool'." -ForegroundColor Cyan

$section = 'system.applicationHost/applicationPools'
$filter = "$section/add[@name='$pool']/environmentVariables"

function Get-PoolVariable([string] $name) {
    $existing = Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter -Name '.' |
        Select-Object -ExpandProperty Collection |
        Where-Object { $_.name -eq $name }
    if ($existing) { $existing.value } else { $null }
}

function Set-PoolVariable([string] $name, [string] $value) {
    $psPath = 'MACHINE/WEBROOT/APPHOST'
    if ($null -ne (Get-PoolVariable $name)) {
        Remove-WebConfigurationProperty -PSPath $psPath -Filter $filter -Name '.' -AtElement @{ name = $name }
    }
    Add-WebConfigurationProperty -PSPath $psPath -Filter $filter -Name '.' -Value @{ name = $name; value = $value }
}

function Ask([string] $setting, [string] $help, [switch] $Secret, [switch] $Optional, [string] $Default) {
    $name = "CardPointe__$setting"
    $current = Get-PoolVariable $name
    $shown = if ($null -eq $current) { if ($Default) { "default: $Default" } else { 'not set' } }
             elseif ($Secret) { 'set' }
             else { $current }
    Write-Host "`n$setting  ($help)" -ForegroundColor Yellow
    if ($Secret) {
        $secure = Read-Host "  [$shown]" -AsSecureString
        $value = [System.Net.NetworkCredential]::new('', $secure).Password
    }
    else {
        $value = Read-Host "  [$shown]"
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        if ($null -eq $current -and $Default) { $value = $Default }
        elseif ($null -ne $current -or $Optional) { return }
        else { throw "$setting is required." }
    }

    Set-PoolVariable $name $value.Trim()
    Write-Host "  saved" -ForegroundColor Green
}

Ask 'MerchantId'  'your CardPointe merchant ID (MID), from CardConnect'
Ask 'ApiUrl'      'e.g. https://<site>.cardconnect.com/cardconnect/rest  (no /auth at the end)'
Ask 'TokenizerUrl' 'e.g. https://<site>.cardconnect.com/itoke/ajax-tokenizer.html'
Ask 'ApiUsername' 'CardPointe Gateway API username' -Secret
Ask 'ApiPassword' 'CardPointe Gateway API password' -Secret
Ask 'Environment' '"production" for live cards, "test" for the UAT gateway' -Default 'production'
Ask 'HostedPageUrl' 'optional: your Hosted Payment Page, e.g. https://<site>.securepayments.cardpointe.com/pay' -Optional
Ask 'HostedPageWebhookKey' 'optional: a long random secret for the hosted page webhook (Enter to generate one)' -Secret -Optional

# A webhook key is useful as soon as there's a hosted page; offer to generate one.
if ((Get-PoolVariable 'CardPointe__HostedPageUrl') -and -not (Get-PoolVariable 'CardPointe__HostedPageWebhookKey')) {
    $bytes = [byte[]]::new(32)
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $key = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    Set-PoolVariable 'CardPointe__HostedPageWebhookKey' $key
    Write-Host "`nGenerated a webhook key. In the HPP Admin's Connect tab set the webhook URL to:" -ForegroundColor Cyan
    Write-Host "  https://$Site/api/pay/hosted/webhook?key=$key"
    Write-Host "(It's shown once here; it's kept in IIS's configuration, not in any file you need to save.)"
}

# Mock payments must never be on in production.
if ((Get-PoolVariable 'CardPointe__UseMockApi') -eq 'true') {
    Remove-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter $filter -Name '.' -AtElement @{ name = 'CardPointe__UseMockApi' }
    Write-Host "`nRemoved CardPointe__UseMockApi=true." -ForegroundColor Yellow
}

Restart-WebAppPool -Name $pool
Write-Host "`nRecycled app pool '$pool'. Check: https://$Site/api/cardpointe/config should now show the merchant ID and tokenizer URL." -ForegroundColor Green
