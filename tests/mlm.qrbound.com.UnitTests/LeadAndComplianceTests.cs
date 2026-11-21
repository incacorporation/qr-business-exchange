using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.UnitTests;

public class LeadScorerTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Lead_without_consent_scores_zero()
    {
        var lead = new Lead { Name = "A", Email = "a@x.com", Interest = LeadInterest.Both, ConsentToContact = false, CreatedUtc = Now };
        Assert.Equal(0, LeadScorer.Score(lead, Now).Score);
    }

    [Fact]
    public void Fresh_referral_with_buying_signals_scores_high()
    {
        var lead = new Lead
        {
            Name = "A", Email = "a@x.com", Interest = LeadInterest.Customer, Source = "referral", Phone = "555",
            Message = "Need pricing for cards for my office team", ConsentToContact = true, CreatedUtc = Now.AddDays(-1),
        };
        var score = LeadScorer.Score(lead, Now);
        Assert.True(score.Score >= 70, $"Expected >= 70, got {score.Score}");
        Assert.StartsWith("Contact today", score.NextAction);
    }

    [Fact]
    public void Old_vague_lead_scores_low()
    {
        var lead = new Lead { Name = "A", Email = "a@x.com", Source = "import", ConsentToContact = true, CreatedUtc = Now.AddDays(-60) };
        Assert.True(LeadScorer.Score(lead, Now).Score < 45);
    }
}

public class ComplianceRulesTests
{
    [Theory]
    [InlineData("Earn $5,000/month from home!", "Income claim")]
    [InlineData("Achieve financial freedom with us", "Lifestyle claim")]
    [InlineData("Fire your boss this year", "Lifestyle claim")]
    [InlineData("It's 100% risk-free", "Guarantee")]
    [InlineData("Build passive income", "Income claim")]
    public void Flags_risky_claims(string text, string category)
    {
        Assert.Contains(ComplianceRules.Check(text), i => i.Category == category);
    }

    [Fact]
    public void Product_copy_passes()
    {
        Assert.Empty(ComplianceRules.Check("Smart NFC business cards that update when your details change. Ask me for a demo."));
    }
}
