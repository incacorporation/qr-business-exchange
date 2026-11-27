namespace mlm.qrbound.com.Domain;

/// <summary>An independent distributor ("member") in the network.</summary>
public class Member
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }

    /// <summary>URL slug for the member's public landing page: /r/{Slug}.</summary>
    public required string Slug { get; set; }

    /// <summary>The member who enrolled this member. Null only for the company root.</summary>
    public int? SponsorId { get; set; }
    public Member? Sponsor { get; set; }
    public List<Member> Frontline { get; set; } = [];

    public string? City { get; set; }
    public string? Bio { get; set; }
    public DateTime JoinedUtc { get; set; } = DateTime.UtcNow;
    public bool IsActiveAccount { get; set; } = true;

    /// <summary>Rank achieved in the most recent commission run.</summary>
    public Rank Rank { get; set; } = Rank.Associate;
}

/// <summary>A retail customer. Customers buy product; they are never recruited into the plan.</summary>
public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public int ReferringMemberId { get; set; }
    public Member? ReferringMember { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Sku { get; set; }
    public string Description { get; set; } = "";
    public decimal Price { get; set; }

    /// <summary>Commissionable volume per unit. Kept separate from price so promotions don't distort pay.</summary>
    public decimal Volume { get; set; }
    public bool IsPublished { get; set; } = true;
}

public enum BuyerType
{
    /// <summary>An outside customer referred by a member. This is the volume the plan rewards.</summary>
    RetailCustomer,

    /// <summary>A member buying for their own use. Counts toward activity only up to a limit.</summary>
    MemberPersonalUse,
}

public enum OrderStatus { Paid, Refunded }

public class Order
{
    public int Id { get; set; }
    public BuyerType BuyerType { get; set; }

    /// <summary>The member credited for the sale (the referrer for customer orders, the buyer for personal orders).</summary>
    public int MemberId { get; set; }
    public Member? Member { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime PlacedUtc { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Paid;
    public decimal Total { get; set; }
    public decimal Volume { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitVolume { get; set; }
}

public enum LeadInterest { Customer, Business, Both, Unknown }
public enum LeadStatus { New, Contacted, Qualified, Converted, Lost }

/// <summary>A prospect captured from a landing page, event, or import.</summary>
public class Lead
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public string Source { get; set; } = "web";
    public LeadInterest Interest { get; set; } = LeadInterest.Unknown;
    public string? Message { get; set; }
    public int? OwnerMemberId { get; set; }
    public Member? OwnerMember { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastContactUtc { get; set; }

    /// <summary>The lead agreed to be contacted. Leads without consent are never messaged.</summary>
    public bool ConsentToContact { get; set; }

    public int Score { get; set; }
    public string? ScoreReason { get; set; }
    public string? NextAction { get; set; }
}

public class CommissionRun
{
    public int Id { get; set; }

    /// <summary>The period paid, as yyyy-MM.</summary>
    public required string Period { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public decimal Revenue { get; set; }
    public decimal TotalPayout { get; set; }

    /// <summary>Factor applied to overrides so total payout stays under the cap. 1 = unscaled.</summary>
    public decimal OverrideScale { get; set; } = 1m;
    public List<CommissionLine> Lines { get; set; } = [];
}

public enum CommissionType { Retail, Override }

public class CommissionLine
{
    public int Id { get; set; }
    public int CommissionRunId { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public CommissionType Type { get; set; }
    public decimal Amount { get; set; }

    /// <summary>For overrides: the paid level after compression (1 = frontline).</summary>
    public int Level { get; set; }
    public int? SourceMemberId { get; set; }
    public Rank RankAtRun { get; set; }
}
