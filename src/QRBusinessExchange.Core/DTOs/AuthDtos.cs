namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// Response after successful authentication
/// </summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = null!;
}


/// <summary>Request a password-reset link by email.</summary>
public class ForgotPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.StringLength(255)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>Set a new password using the token from a password-reset link.</summary>
public class ResetPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(2048)]
    public string Token { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
