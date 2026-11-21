using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

// ---------------------------------------------------------------------------
// "Link a phone": a signed-in computer shows a QR code; the phone scans it,
// the user confirms the number on the computer, and the phone creates its own
// passkey for the same account (and is signed in).
// ---------------------------------------------------------------------------

public static class DeviceLinkStatus
{
    /// <summary>QR shown, nobody scanned yet.</summary>
    public const string Pending = "Pending";
    /// <summary>A phone opened the link; waiting for the number to be confirmed on the computer.</summary>
    public const string Scanned = "Scanned";
    /// <summary>The computer confirmed the number; the phone may now create its passkey.</summary>
    public const string Confirmed = "Confirmed";
    /// <summary>The phone's passkey was saved.</summary>
    public const string Completed = "Completed";
    /// <summary>Cancelled on either side, or the wrong number was picked.</summary>
    public const string Denied = "Denied";
    public const string Expired = "Expired";
}

/// <summary>[Computer] Result of starting a phone link.</summary>
public class DeviceLinkCreateResponse
{
    public Guid SessionId { get; set; }

    /// <summary>URL to encode in the QR code. Contains a one-time secret in the fragment.</summary>
    public string LinkUrl { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public int PollIntervalSeconds { get; set; }
}

/// <summary>[Computer] Current state of a phone link.</summary>
public class DeviceLinkStatusDto
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>User agent / IP of the phone that scanned, once scanned.</summary>
    public string? PhoneUserAgent { get; set; }
    public string? PhoneIp { get; set; }

    /// <summary>Numbers to choose from on the computer (only while <c>Scanned</c>).</summary>
    public List<string> VerificationChoices { get; set; } = new();

    /// <summary>Name of the passkey that was created (once <c>Completed</c>).</summary>
    public string? DeviceName { get; set; }
}

/// <summary>[Computer] The number the user picked.</summary>
public class DeviceLinkConfirmRequest
{
    [Required]
    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>[Phone] Claim the link using the secret from the QR code.</summary>
public class DeviceLinkClaimRequest
{
    [Required]
    public string LinkSecret { get; set; } = string.Empty;
}

/// <summary>[Phone] Returned after claiming.</summary>
public class DeviceLinkClaimResponse
{
    /// <summary>Secret the phone uses for every following call.</summary>
    public string PhoneSecret { get; set; } = string.Empty;

    /// <summary>Number the phone displays; the user picks it on the computer.</summary>
    public string VerificationCode { get; set; } = string.Empty;

    /// <summary>Masked email of the account being linked, e.g. j***@example.com.</summary>
    public string AccountHint { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public int PollIntervalSeconds { get; set; }
}

/// <summary>[Phone] Any call made after claiming.</summary>
public class DeviceLinkPhoneRequest
{
    [Required]
    public string PhoneSecret { get; set; } = string.Empty;
}

public class DeviceLinkPhoneStatusDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class DeviceLinkRegisterInitRequest : DeviceLinkPhoneRequest
{
    public string? DeviceName { get; set; }
}

public class DeviceLinkRegisterCompleteRequest : DeviceLinkPhoneRequest
{
    [Required]
    public string Challenge { get; set; } = string.Empty;

    [Required]
    public CredentialResponseDto Credential { get; set; } = new();

    public string? DeviceName { get; set; }
}
