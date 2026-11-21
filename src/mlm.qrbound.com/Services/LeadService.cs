using Microsoft.EntityFrameworkCore;
using mlm.qrbound.com.Data;
using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.Services;

public class LeadService(IDbContextFactory<MlmDbContext> dbFactory, TimeProvider time)
{
    public async Task<Lead> CaptureAsync(Lead lead)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        lead.Email = lead.Email.Trim().ToLowerInvariant();
        lead.CreatedUtc = time.GetUtcNow().UtcDateTime;
        ApplyScore(lead);
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return lead;
    }

    public async Task<List<Lead>> GetLeadsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var leads = await db.Leads.Include(l => l.OwnerMember).ToListAsync();
        // Re-score on read: recency changes daily, so stored scores would go stale.
        foreach (var l in leads.Where(l => l.ScoreReason is null || !l.ScoreReason.StartsWith("AI:")))
            ApplyScore(l);
        await db.SaveChangesAsync();
        return leads.OrderByDescending(l => l.Score).ThenByDescending(l => l.CreatedUtc).ToList();
    }

    public async Task UpdateAsync(int id, Action<Lead> change)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var lead = await db.Leads.FindAsync(id) ?? throw new InvalidOperationException($"Lead {id} not found.");
        change(lead);
        await db.SaveChangesAsync();
    }

    private void ApplyScore(Lead lead)
    {
        var s = LeadScorer.Score(lead, time.GetUtcNow().UtcDateTime);
        lead.Score = s.Score;
        lead.ScoreReason = s.Reason;
        lead.NextAction = s.NextAction;
    }
}
