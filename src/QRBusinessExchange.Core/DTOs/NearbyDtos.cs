namespace QRBusinessExchange.Core.DTOs;

/// <summary>Someone with Ping Me on, near the person searching.</summary>
public class NearbyUserDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Only when the person shows it on their business card.</summary>
    public string? CompanyName { get; set; }

    /// <summary>Only when the person shows it on their business card.</summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>Rounded to about 10 m.</summary>
    public double Latitude { get; set; }

    /// <summary>Rounded to about 10 m.</summary>
    public double Longitude { get; set; }

    public double DistanceMiles { get; set; }

    /// <summary>When the person's location was last shared.</summary>
    public DateTime LastSeen { get; set; }

    /// <summary>The person is in the searcher's contacts.</summary>
    public bool IsContact { get; set; }
}

/// <summary>The signed-in user's Ping Me setting.</summary>
public class PingMeStatusDto
{
    /// <summary>On: your location is shared and you appear on nearby maps.</summary>
    public bool Enabled { get; set; }

    /// <summary>When your location was last shared (null if never).</summary>
    public DateTime? LastUpdated { get; set; }
}

/// <summary>Share your current position; optionally turn Ping Me on or off at the same time.</summary>
public class ShareLocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>True turns Ping Me on, false turns it off, null leaves it as it is.</summary>
    public bool? PingMeEnabled { get; set; }
}

/// <summary>Turn Ping Me on or off.</summary>
public class SetPingMeRequest
{
    public bool Enabled { get; set; }
}
