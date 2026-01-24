using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// DTO for creating a business connection request
/// </summary>
public class ConnectionRequestDto
{
    /// <summary>
    /// Unique identifier of the recipient user
    /// </summary>
    [Required(ErrorMessage = "Recipient user ID is required")]
    public Guid RecipientUserId { get; set; }

    /// <summary>
    /// Optional message to accompany the connection request
    /// </summary>
    [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters")]
    public string? Message { get; set; }
}
