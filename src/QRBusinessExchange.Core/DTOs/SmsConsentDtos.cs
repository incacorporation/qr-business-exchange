namespace QRBusinessExchange.Core.DTOs;

/// <summary>The signed-in user's consent to account and security text messages.</summary>
public class SmsConsentDto
{
    public bool OptIn { get; set; }

    /// <summary>When the user agreed, if they have.</summary>
    public DateTime? OptInAt { get; set; }

    /// <summary>The number on the user's profile, which texts would go to (null when there is none).</summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>Turns text messages on (the user ticked the box) or off.</summary>
public class SetSmsConsentRequest
{
    public bool OptIn { get; set; }
}
