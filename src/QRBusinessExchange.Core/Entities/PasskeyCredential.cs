using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents WebAuthn/FIDO2 passkey credentials for passwordless authentication
/// </summary>
public class PasskeyCredential
{
    [Key]
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    public int Counter { get; set; } = 0;

    [StringLength(255)]
    public string? DeviceName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsed { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
