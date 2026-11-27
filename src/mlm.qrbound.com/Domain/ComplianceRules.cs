using System.Text.RegularExpressions;

namespace mlm.qrbound.com.Domain;

public record ComplianceIssue(string Phrase, string Category, string Why);

/// <summary>
/// Rule-based screen for the claims regulators (e.g. the US FTC) most often cite in direct-selling
/// marketing: income and lifestyle claims, guarantees, and health claims. The AI review adds nuance
/// on top, but these rules always run, so a missing API key never lets a risky post through.
/// </summary>
public static partial class ComplianceRules
{
    private static readonly (Regex Pattern, string Category, string Why)[] Rules =
    [
        (Rx(@"\$\s?\d[\d,]*(\.\d+)?\s*(k|K)?\s*(/|per|a|an|each)\s*(month|mo|week|wk|year|yr|day)"), "Income claim",
            "Specific earnings figures imply typical results; most participants earn far less."),
        (Rx(@"\b(six|seven)[- ]figure"), "Income claim", "Implies typical participants reach very high incomes."),
        (Rx(@"\bfinancial(ly)? (freedom|free|independence)"), "Lifestyle claim", "Lifestyle claims are treated as income claims."),
        (Rx(@"\b(quit|fire) your (job|boss)"), "Lifestyle claim", "Implies the business replaces a full-time income."),
        (Rx(@"\b(passive|residual) income"), "Income claim", "Suggests earnings without ongoing work."),
        (Rx(@"\b(retire early|early retirement|be your own boss and get rich)"), "Lifestyle claim", "Lifestyle claim tied to earnings."),
        (Rx(@"\b(guarantee[ds]?|risk[- ]free|can'?t lose|no risk)"), "Guarantee", "Results can't be guaranteed."),
        (Rx(@"\b(get rich|make money fast|easy money|unlimited income)"), "Income claim", "Exaggerated earnings language."),
        (Rx(@"\b(cures?|treats?|heals?|prevents?)\b\s+\w+"), "Health claim", "Disease claims need regulatory approval."),
        (Rx(@"\b(lambo|lamborghini|ferrari|mansion|yacht)"), "Lifestyle claim", "Luxury imagery implies typical earnings."),
        (Rx(@"\brecruit(ing)? (bonus|pays)|paid (to|for) recruit"), "Recruitment pay", "Pay must come from product sales, not recruiting."),
    ];

    public static IReadOnlyList<ComplianceIssue> Check(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var issues = new List<ComplianceIssue>();
        foreach (var (pattern, category, why) in Rules)
            foreach (Match m in pattern.Matches(text))
                issues.Add(new ComplianceIssue(m.Value.Trim(), category, why));
        return issues;
    }

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
