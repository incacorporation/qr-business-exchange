using Microsoft.EntityFrameworkCore;
using mlm.qrbound.com.Data;
using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.Services;

public record DashboardStats(
    int Members, int ActiveMembers, int Customers, int OpenLeads,
    decimal RevenueThisMonth, decimal RetailShareThisMonth, decimal RevenueLastMonth,
    PlanResult Preview);

public record IncomeBand(Rank Rank, int People, decimal Median, decimal Mean, decimal Top10Percent);

public record IncomeDisclosure(string FromPeriod, string ToPeriod, int Participants, decimal PercentEarningNothing,
    decimal MedianAll, IReadOnlyList<IncomeBand> Bands);

public record GenealogyNode(Member Member, MemberResult? Stats, List<GenealogyNode> Children);

/// <summary>Reads and writes the network: dashboard numbers, genealogy, commission runs and income disclosure.</summary>
public class NetworkService(IDbContextFactory<MlmDbContext> dbFactory, CompensationEngine engine)
{
    public static string PeriodOf(DateTime utc) => utc.ToString("yyyy-MM");

    public static (DateTime Start, DateTime End) PeriodRange(string period)
    {
        var start = DateTime.SpecifyKind(DateTime.ParseExact(period + "-01", "yyyy-MM-dd", null), DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    public async Task<PlanResult> PreviewAsync(string period)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await CalculateAsync(db, period);
    }

    private async Task<PlanResult> CalculateAsync(MlmDbContext db, string period)
    {
        var (start, end) = PeriodRange(period);
        var members = await db.Members.AsNoTracking().Select(m => new PlanMember(m.Id, m.SponsorId)).ToListAsync();
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.PlacedUtc >= start && o.PlacedUtc < end)
            .Select(o => new PlanOrder(o.MemberId, o.BuyerType, o.Total, o.Volume, o.Status))
            .ToListAsync();
        return engine.Calculate(members, orders);
    }

    public async Task<DashboardStats> GetDashboardAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var period = PeriodOf(now);
        var preview = await CalculateAsync(db, period);

        var (lastStart, lastEnd) = PeriodRange(PeriodOf(now.AddMonths(-1)));
        var lastTotals = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid && o.PlacedUtc >= lastStart && o.PlacedUtc < lastEnd)
            .Select(o => o.Total).ToListAsync();

        var (start, end) = PeriodRange(period);
        var thisMonth = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid && o.PlacedUtc >= start && o.PlacedUtc < end)
            .Select(o => new { o.Total, o.BuyerType }).ToListAsync();
        var revenue = thisMonth.Sum(o => o.Total);
        var retail = thisMonth.Where(o => o.BuyerType == BuyerType.RetailCustomer).Sum(o => o.Total);

        return new DashboardStats(
            await db.Members.CountAsync(m => m.SponsorId != null),
            preview.Members.Values.Count(m => m.Active),
            await db.Customers.CountAsync(),
            await db.Leads.CountAsync(l => l.Status == LeadStatus.New || l.Status == LeadStatus.Contacted || l.Status == LeadStatus.Qualified),
            revenue,
            revenue == 0 ? 0 : retail / revenue,
            lastTotals.Sum(),
            preview);
    }

    public async Task<GenealogyNode?> GetGenealogyAsync(string period)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var members = await db.Members.AsNoTracking().ToListAsync();
        var stats = (await CalculateAsync(db, period)).Members;
        var byParent = members.Where(m => m.SponsorId != null).ToLookup(m => m.SponsorId!.Value);
        var root = members.FirstOrDefault(m => m.SponsorId == null);
        if (root is null) return null;

        GenealogyNode Build(Member m) => new(m, stats.GetValueOrDefault(m.Id),
            byParent[m.Id].OrderByDescending(c => stats.GetValueOrDefault(c.Id)?.GroupVolume ?? 0).Select(Build).ToList());
        return Build(root);
    }

    public async Task<List<CommissionRun>> GetRunsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CommissionRuns.AsNoTracking().OrderByDescending(r => r.Period).ToListAsync();
    }

    public async Task<List<CommissionLine>> GetRunLinesAsync(int runId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CommissionLines.AsNoTracking().Include(l => l.Member)
            .Where(l => l.CommissionRunId == runId).ToListAsync();
    }

    /// <summary>Calculates and stores the commission run for a period, replacing any earlier run for it.</summary>
    public async Task<CommissionRun> RunCommissionsAsync(string period)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var result = await CalculateAsync(db, period);

        await db.CommissionRuns.Where(r => r.Period == period).ExecuteDeleteAsync();
        var run = new CommissionRun
        {
            Period = period,
            Revenue = result.Revenue,
            TotalPayout = result.TotalPayout,
            OverrideScale = result.OverrideScale,
            Lines = result.Lines.Select(l => new CommissionLine
            {
                MemberId = l.MemberId, Type = l.Type, Amount = l.Amount, Level = l.Level,
                SourceMemberId = l.SourceMemberId, RankAtRun = result.Members[l.MemberId].Rank,
            }).ToList(),
        };
        db.CommissionRuns.Add(run);

        // Members' displayed rank follows the latest run.
        if (period == (await db.CommissionRuns.Select(r => r.Period).ToListAsync()).Append(period).Max())
        {
            foreach (var m in await db.Members.ToListAsync())
                m.Rank = result.Members.TryGetValue(m.Id, out var s) ? s.Rank : Rank.Member;
        }

        await db.SaveChangesAsync();
        return run;
    }

    /// <summary>
    /// Annualised earnings by rank from stored commission runs. Publishing this openly is the
    /// single most important honesty feature an MLM can offer prospects.
    /// </summary>
    public async Task<IncomeDisclosure?> GetIncomeDisclosureAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var runs = await db.CommissionRuns.AsNoTracking().OrderBy(r => r.Period).ToListAsync();
        if (runs.Count == 0) return null;

        var memberIds = await db.Members.AsNoTracking().Where(m => m.SponsorId != null).Select(m => new { m.Id, m.Rank }).ToListAsync();
        var lines = await db.CommissionLines.AsNoTracking().Select(l => new { l.MemberId, l.Amount }).ToListAsync();
        var annualFactor = 12m / runs.Count;
        var earnings = memberIds.ToDictionary(
            m => m.Id,
            m => lines.Where(l => l.MemberId == m.Id).Sum(l => l.Amount) * annualFactor);

        var all = earnings.Values.OrderBy(v => v).ToList();
        var bands = memberIds.GroupBy(m => m.Rank).OrderBy(g => g.Key).Select(g =>
        {
            var vals = g.Select(m => earnings[m.Id]).OrderBy(v => v).ToList();
            var top = vals.Skip((int)Math.Floor(vals.Count * 0.9)).DefaultIfEmpty(0m).Min();
            return new IncomeBand(g.Key, vals.Count, Median(vals), vals.Count == 0 ? 0 : vals.Average(), top);
        }).ToList();

        return new IncomeDisclosure(runs.First().Period, runs.Last().Period, all.Count,
            all.Count == 0 ? 0 : (decimal)all.Count(v => v == 0) / all.Count, Median(all), bands);
    }

    private static decimal Median(List<decimal> sorted) =>
        sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;

    public async Task<List<Member>> GetMembersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Members.AsNoTracking().Include(m => m.Sponsor).OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<Member?> GetMemberBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Slug == slug && m.IsActiveAccount);
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Products.AsNoTracking().Where(p => p.IsPublished).OrderBy(p => p.Id).ToListAsync();
    }

    public async Task<Product?> GetProductAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);
    }
}
