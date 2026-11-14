using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// A user's uploaded picture (a personal photo or a business logo). Kept in the database so it
/// survives redeploys; served by GET api/user/{id}/image.
/// </summary>
public class ProfileImage
{
    [Key]
    public Guid UserId { get; set; }

    /// <summary>image/jpeg, image/png or image/gif, taken from the file's bytes rather than its name.</summary>
    [Required]
    [StringLength(50)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public byte[] Data { get; set; } = [];

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}
