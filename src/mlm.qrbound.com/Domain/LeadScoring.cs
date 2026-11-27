namespace mlm.qrbound.com.Domain;

public record LeadScore(int Score, string Reason, string NextAction);

/// <summary>
/// Fast, explainable lead scoring that works with no AI configured. The AI assistant refines
/// these results when an API key is available; this scorer is the always-on baseline.
/// </summary>
public static class LeadScorer
{
    private static readonly string[] BuyingSignals =
        ["price", "pricing", "cost", "order", "buy", "team", "office", "staff", "bundle", "need", "cards", "quote"];

    public static LeadScore Score(Lead lead, DateTime nowUtc)
    {
        if (!lead.ConsentToContact)
            return new LeadScore(0, "No consent to contact.", "Do not contact. Ask for consent only if they reach out again.");

        var score = 20;
        var reasons = new List<string>();

        switch (lead.Interest)
        {
            case LeadInterest.Customer: score += 20; reasons.Add("wants the product"); break;
            case LeadInterest.Both: score += 25; reasons.Add("interested in product and business"); break;
            case LeadInterest.Business: score += 12; reasons.Add("interested in the business"); break;
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone)) { score += 8; reasons.Add("left a phone number"); }

        var message = lead.Message?.ToLowerInvariant() ?? "";
        if (message.Length > 25) { score += 7; reasons.Add("wrote a detailed note"); }
        if (BuyingSignals.Any(message.Contains)) { score += 15; reasons.Add("buying signals in note"); }

        switch (lead.Source)
        {
            case "referral": score += 15; reasons.Add("referred"); break;
            case "event": score += 10; reasons.Add("met in person"); break;
            case "landing-page": score += 6; reasons.Add("came through a landing page"); break;
        }

        var age = (nowUtc - lead.CreatedUtc).TotalDays;
        if (age < 3) { score += 15; reasons.Add("new in the last 3 days"); }
        else if (age < 14) score += 8;
        else if (age > 30) { score -= 10; reasons.Add("over 30 days old"); }

        if (lead.Status is LeadStatus.Converted or LeadStatus.Lost) score = Math.Min(score, 10);

        score = Math.Clamp(score, 0, 100);
        var next = lead.Status switch
        {
            LeadStatus.Converted => "Already converted. Check in after their first order.",
            LeadStatus.Lost => "Closed. No action.",
            _ when score >= 70 => "Contact today. Lead with the product that fits their note.",
            _ when score >= 45 => "Send a short personal follow-up this week.",
            _ => "Add to the monthly newsletter; no direct outreach yet.",
        };

        return new LeadScore(score, reasons.Count == 0 ? "Little information yet." : string.Join(", ", reasons) + ".", next);
    }
}
