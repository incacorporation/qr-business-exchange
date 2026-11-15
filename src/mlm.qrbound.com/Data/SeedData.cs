using Microsoft.EntityFrameworkCore;
using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.Data;

/// <summary>Creates a small, deterministic demo network so every page has data on first run.</summary>
public static class SeedData
{
    private static readonly string[] FirstNames =
        ["Ava", "Liam", "Mia", "Noah", "Zoe", "Ethan", "Ivy", "Lucas", "Nora", "Owen", "Ruby", "Eli",
         "Lena", "Jack", "Maya", "Leo", "Iris", "Max", "Ella", "Sam", "Aria", "Ben", "Cora", "Dan",
         "Faye", "Gus", "Hana", "Ian", "June", "Kai"];
    private static readonly string[] LastNames =
        ["Garcia", "Nguyen", "Patel", "Smith", "Kim", "Lopez", "Brown", "Davis", "Martin", "Wilson"];
    private static readonly string[] Cities =
        ["Austin, TX", "Denver, CO", "Phoenix, AZ", "Tampa, FL", "Raleigh, NC", "Columbus, OH", "Boise, ID", "Omaha, NE"];

    public static async Task EnsureSeededAsync(MlmDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Members.AnyAsync()) return;

        var rng = new Random(42);

        var products = new List<Product>
        {
            new() { Name = "QR Smart Business Card Pack", Slug = "qr-smart-business-cards", Sku = "QR-CARD-50", Price = 39m, Volume = 30m,
                Description = "50 premium NFC + QR business cards linked to your live digital profile. Update your details any time without reprinting." },
            new() { Name = "Digital Profile Pro (1 year)", Slug = "digital-profile-pro", Sku = "QR-PRO-12", Price = 96m, Volume = 80m,
                Description = "A year of the Pro digital profile: custom domain link, lead capture form, analytics and passkey sign-in." },
            new() { Name = "Event Lead Kit", Slug = "event-lead-kit", Sku = "QR-EVT-01", Price = 149m, Volume = 120m,
                Description = "Table-top QR stand, 200 cards and a scan-to-follow-up workflow for trade shows and networking events." },
            new() { Name = "Team Starter Bundle", Slug = "team-starter-bundle", Sku = "QR-TEAM-05", Price = 249m, Volume = 200m,
                Description = "Five Digital Profile Pro seats with shared branding — ideal for small offices and sales teams." },
        };
        db.Products.AddRange(products);

        var root = new Member
        {
            Name = "QRBound Company", Email = "company@mlm.qrbound.com", Slug = "qrbound",
            City = "Austin, TX", Bio = "Corporate account.", JoinedUtc = DateTime.UtcNow.AddYears(-2),
        };
        db.Members.Add(root);

        var members = new List<Member> { root };
        for (var i = 0; i < 30; i++)
        {
            var first = FirstNames[i];
            var last = LastNames[rng.Next(LastNames.Length)];
            // Early members sponsor more people, giving a realistic, uneven tree.
            var sponsor = i < 4 ? root : members[1 + rng.Next(Math.Min(members.Count - 1, 4 + i / 2))];
            var m = new Member
            {
                Name = $"{first} {last}",
                Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.com",
                Slug = $"{first}-{last}".ToLowerInvariant(),
                Sponsor = sponsor,
                City = Cities[rng.Next(Cities.Length)],
                Bio = $"Helping local businesses go paperless with QR and NFC networking tools.",
                JoinedUtc = DateTime.UtcNow.AddDays(-rng.Next(30, 600)),
            };
            members.Add(m);
            db.Members.Add(m);
        }

        var customers = new List<Customer>();
        foreach (var m in members.Skip(1))
        {
            var count = rng.Next(0, 7);
            for (var c = 0; c < count; c++)
            {
                var cust = new Customer
                {
                    Name = $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
                    Email = $"customer{customers.Count + 1}@example.net",
                    ReferringMember = m,
                    CreatedUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 200)),
                };
                customers.Add(cust);
                db.Customers.Add(cust);
            }
        }

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var monthsBack = 2; monthsBack >= 0; monthsBack--)
        {
            var start = monthStart.AddMonths(-monthsBack);
            var days = monthsBack == 0 ? Math.Max(1, DateTime.UtcNow.Day) : DateTime.DaysInMonth(start.Year, start.Month);

            foreach (var cust in customers)
            {
                if (rng.NextDouble() < 0.45) continue;
                db.Orders.Add(MakeOrder(BuyerType.RetailCustomer, cust.ReferringMember!, cust, products, rng, start, days));
            }
            foreach (var m in members.Skip(1))
            {
                if (rng.NextDouble() < 0.5)
                    db.Orders.Add(MakeOrder(BuyerType.MemberPersonalUse, m, null, products, rng, start, days));
            }
        }

        var sources = new[] { "landing-page", "event", "referral", "social", "import" };
        var notes = new[]
        {
            "Saw the card at a chamber of commerce mixer. Wants pricing for 10 people.",
            "Interested in the side business. Asked how much time it takes per week.",
            "Realtor, needs cards that update when listings change.",
            null,
            "Just browsing.",
            "Runs a dental office, wants the Team bundle for front desk staff.",
        };
        for (var i = 0; i < 18; i++)
        {
            var owner = members[1 + rng.Next(members.Count - 1)];
            db.Leads.Add(new Lead
            {
                Name = $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
                Email = $"lead{i + 1}@example.org",
                Phone = rng.NextDouble() < 0.6 ? $"555-01{rng.Next(10, 99)}" : null,
                Source = sources[rng.Next(sources.Length)],
                Interest = (LeadInterest)rng.Next(0, 4),
                Message = notes[rng.Next(notes.Length)],
                OwnerMember = owner,
                ConsentToContact = rng.NextDouble() < 0.85,
                CreatedUtc = DateTime.UtcNow.AddDays(-rng.Next(0, 45)),
            });
        }

        await db.SaveChangesAsync();
    }

    private static Order MakeOrder(BuyerType type, Member member, Customer? customer, List<Product> products, Random rng, DateTime start, int days)
    {
        var order = new Order
        {
            BuyerType = type,
            Member = member,
            Customer = customer,
            PlacedUtc = start.AddDays(rng.Next(0, days)).AddHours(rng.Next(0, 23)),
            Status = rng.NextDouble() < 0.03 ? OrderStatus.Refunded : OrderStatus.Paid,
        };
        var lineCount = rng.Next(1, 3);
        foreach (var p in products.OrderBy(_ => rng.Next()).Take(lineCount))
        {
            var qty = rng.Next(1, 3);
            order.Lines.Add(new OrderLine { Product = p, Quantity = qty, UnitPrice = p.Price, UnitVolume = p.Volume });
            order.Total += p.Price * qty;
            order.Volume += p.Volume * qty;
        }
        return order;
    }
}
