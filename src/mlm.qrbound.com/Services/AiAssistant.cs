using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.Services;

public class AiOptions
{
    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>Optional. When empty the SDK reads ANTHROPIC_API_KEY or an `ant auth login` profile.</summary>
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

public record AiLeadInsight(int Score, string Reason, string NextAction);
public record AiComplianceReview(bool Compliant, List<AiComplianceFinding> Findings, string CompliantRewrite);
public record AiComplianceFinding(string Phrase, string Category, string Why);
public record AiSeoCopy(string Title, string MetaDescription, string Headline, string Body);

/// <summary>
/// Claude-backed helpers: lead insight, outreach drafts, compliance review and SEO copy.
/// Every prompt carries the same compliance rules, so the AI never writes income or health claims.
/// </summary>
public class AiAssistant(AiOptions options, ILogger<AiAssistant> logger)
{
    private const string Rules = """
        You work for mlm.qrbound.com, a direct-selling company that sells QR and NFC digital business cards
        and profile subscriptions through independent members. Follow these rules in everything you write:
        - Never state or imply earnings, income figures, "financial freedom", quitting a job, passive income,
          luxury lifestyles or guaranteed results. If income comes up, point to the published income disclosure
          at /income-disclosure.
        - Lead with the product and the customer's problem, not with recruiting.
        - Make no health, medical or legal claims.
        - Respect consent: write only to people who agreed to be contacted, and include an easy opt-out line in outreach.
        - Be warm, plain and brief. No hype, no ALL CAPS, no pressure tactics or fake urgency.
        """;

    private readonly AnthropicClient? _client = CreateClient(options);

    public bool IsAvailable => _client is not null;

    private static AnthropicClient? CreateClient(AiOptions o)
    {
        if (!o.Enabled) return null;
        var key = string.IsNullOrWhiteSpace(o.ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : o.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return null;
        return new AnthropicClient { ApiKey = key };
    }

    public async Task<AiLeadInsight?> ScoreLeadAsync(Lead lead, LeadScore baseline, CancellationToken ct = default)
    {
        var prompt = $"""
            Score this sales lead from 0 to 100 for how likely they are to buy in the next 30 days,
            and say the single best next action. A rule-based scorer gave {baseline.Score} ({baseline.Reason});
            adjust it only if the details justify it.

            Name: {lead.Name}
            Interest: {lead.Interest}
            Source: {lead.Source}
            Status: {lead.Status}
            Has phone: {!string.IsNullOrWhiteSpace(lead.Phone)}
            Days since captured: {(DateTime.UtcNow - lead.CreatedUtc).TotalDays:0}
            Their note: {lead.Message ?? "(none)"}
            """;
        return await AskJsonAsync<AiLeadInsight>(prompt, new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "score", "reason", "nextAction" },
            properties = new
            {
                score = new { type = "integer" },
                reason = new { type = "string", description = "One sentence." },
                nextAction = new { type = "string", description = "One concrete action." },
            },
        }, Effort.Low, ct);
    }

    public async Task<string?> DraftOutreachAsync(Lead lead, string memberName, string channel, CancellationToken ct = default)
    {
        if (!lead.ConsentToContact) return null;
        var prompt = $"""
            Write a short {channel} follow-up from {memberName} to {lead.Name}, who came in via {lead.Source}
            and is interested in: {lead.Interest}. Their note: "{lead.Message ?? "none"}".
            Keep it under 90 words, personal, product-first, with one clear question and an opt-out line.
            Return only the message text.
            """;
        return await AskTextAsync(prompt, Effort.Low, ct);
    }

    public async Task<AiComplianceReview?> ReviewComplianceAsync(string copy, CancellationToken ct = default)
    {
        var prompt = $"""
            Review this marketing text written by an independent member for compliance with direct-selling
            advertising rules (FTC Business Opportunity Rule and guidance on earnings and health claims).
            List each problematic phrase, its category and why it's a problem, then rewrite the whole text
            so it keeps the member's voice and intent but is compliant.

            <text>
            {copy}
            </text>
            """;
        return await AskJsonAsync<AiComplianceReview>(prompt, new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "compliant", "findings", "compliantRewrite" },
            properties = new
            {
                compliant = new { type = "boolean" },
                findings = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "phrase", "category", "why" },
                        properties = new
                        {
                            phrase = new { type = "string" },
                            category = new { type = "string" },
                            why = new { type = "string" },
                        },
                    },
                },
                compliantRewrite = new { type = "string" },
            },
        }, Effort.Medium, ct);
    }

    public async Task<AiSeoCopy?> DraftLandingCopyAsync(Member member, string focusKeyword, CancellationToken ct = default)
    {
        var prompt = $"""
            Write SEO copy for {member.Name}'s personal landing page on mlm.qrbound.com.
            Location: {member.City ?? "not given"}. Their bio: {member.Bio ?? "none"}.
            Target search phrase: "{focusKeyword}".
            Title: at most 60 characters, includes the phrase and the city if given.
            Meta description: 140-155 characters, one benefit and a call to action.
            Headline: at most 70 characters. Body: 2 short paragraphs of plain text for local business owners.
            """;
        return await AskJsonAsync<AiSeoCopy>(prompt, new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "title", "metaDescription", "headline", "body" },
            properties = new
            {
                title = new { type = "string" },
                metaDescription = new { type = "string" },
                headline = new { type = "string" },
                body = new { type = "string" },
            },
        }, Effort.Low, ct);
    }

    private async Task<string?> AskTextAsync(string prompt, Effort effort, CancellationToken ct)
    {
        var message = await SendAsync(prompt, effort, null, ct);
        return message is null ? null : string.Concat(message.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text)).Trim();
    }

    private async Task<T?> AskJsonAsync<T>(string prompt, object schema, Effort effort, CancellationToken ct) where T : class
    {
        var schemaElement = JsonSerializer.SerializeToElement(schema);
        var format = new BetaJsonOutputFormat
        {
            Schema = schemaElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
        };
        var message = await SendAsync(prompt, effort, format, ct);
        var json = message is null ? null : string.Concat(message.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Claude returned JSON that didn't match {Type}", typeof(T).Name);
            return null;
        }
    }

    private async Task<BetaMessage?> SendAsync(string prompt, Effort effort, BetaJsonOutputFormat? format, CancellationToken ct)
    {
        if (_client is null) return null;
        try
        {
            var message = await _client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                // If a safety classifier declines, the API re-serves the request on a fallback model.
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
                MaxTokens = 16000,
                System = Rules,
                OutputConfig = new BetaOutputConfig { Effort = effort, Format = format },
                Messages = [new BetaUserMessageParam { Content = prompt }],
            }, ct);

            if (message.StopReason == BetaStopReason.Refusal)
            {
                logger.LogWarning("Claude declined an assistant request");
                return null;
            }
            return message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // AI features are optional: log and let the caller fall back to the rule-based result.
            logger.LogError(ex, "Claude request failed");
            return null;
        }
    }
}
