namespace QRBusinessExchange.Core.DTOs;

/// <summary>A saved contact: the person's public business-card details plus when they were added.</summary>
public class ContactDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime AddedAt { get; set; }
}

/// <summary>Add the person with this user ID (from their business-card QR code) to your contacts.</summary>
public class AddContactRequest
{
    public Guid UserId { get; set; }
}
