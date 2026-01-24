using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents a business connection request between two users
/// </summary>
public class Connection
{
    [Key]
    public Guid ConnectionId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InitiatorUserId { get; set; }

    [Required]
    public Guid RecipientUserId { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending"; // Pending/Accepted/Rejected

    public bool CreditDeducted { get; set; } = false;

    public DateTime ConnectionDate { get; set; } = DateTime.UtcNow;

    public DateTime? ResponseDate { get; set; }

    public string? Message { get; set; }

    // Navigation properties
    public virtual User InitiatorUser { get; set; } = null!;
    public virtual User RecipientUser { get; set; } = null!;
}
