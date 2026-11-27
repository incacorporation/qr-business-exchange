namespace QRBusinessExchange.Client.Services;

/// <summary>
/// Industries and business skills offered in the Skills picker on Edit Profile.
/// Users can still add their own skills; this list only makes the common ones one tap away.
/// </summary>
public static class SkillCatalog
{
    public sealed record Group(string Name, string Icon, IReadOnlyList<string> Skills);

    public static IReadOnlyList<Group> Groups { get; } =
    [
        new("Industries", "bi-buildings",
        [
            "Accounting", "Agriculture", "Architecture", "Automotive", "Banking", "Construction", "Consulting",
            "Education", "Energy", "Entertainment", "Fashion", "Financial Services", "Food & Beverage",
            "Government", "Healthcare", "Hospitality", "Insurance", "Legal", "Logistics", "Manufacturing",
            "Media", "Nonprofit", "Pharmaceuticals", "Real Estate", "Retail", "Software", "Telecommunications",
            "Travel & Tourism"
        ]),
        new("Technology", "bi-cpu",
        [
            "Web Development", "Mobile Apps", "Software Engineering", "Cloud Computing", "Cybersecurity",
            "Data Analysis", "Data Science", "Artificial Intelligence", "DevOps", "Database Administration",
            "IT Support", "Networking", "UI/UX Design", "Quality Assurance", "E-commerce Platforms"
        ]),
        new("Marketing & Sales", "bi-megaphone",
        [
            "Marketing", "Digital Marketing", "Social Media", "SEO", "Content Writing", "Copywriting",
            "Email Marketing", "Brand Strategy", "Public Relations", "Advertising", "Market Research",
            "Sales", "Business Development", "Lead Generation", "Account Management", "Customer Success"
        ]),
        new("Creative", "bi-palette",
        [
            "Design", "Graphic Design", "Photography", "Video Production", "Animation", "Illustration",
            "Interior Design", "Event Planning", "Music", "Writing & Editing", "Translation"
        ]),
        new("Business & Finance", "bi-briefcase",
        [
            "Management", "Project Management", "Operations", "Strategy", "Entrepreneurship",
            "Bookkeeping", "Tax Preparation", "Financial Planning", "Investment", "Fundraising",
            "Procurement", "Supply Chain", "Human Resources", "Recruiting", "Training & Coaching",
            "Customer Service"
        ]),
        new("Professional & Trades", "bi-tools",
        [
            "Legal Advice", "Compliance", "Real Estate Agent", "Property Management", "Engineering",
            "Electrical", "Plumbing", "Carpentry", "HVAC", "Landscaping", "Cleaning Services",
            "Fitness & Wellness", "Nursing", "Tutoring"
        ])
    ];

    /// <summary>Splits a comma-separated skills value into trimmed, de-duplicated entries.</summary>
    public static List<string> Parse(string? value) =>
        (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Joins skills back into the comma-separated form stored on the profile.</summary>
    public static string Join(IEnumerable<string> skills) => string.Join(", ", skills);
}
