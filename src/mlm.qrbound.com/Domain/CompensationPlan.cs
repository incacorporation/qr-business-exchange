namespace mlm.qrbound.com.Domain;

public enum Rank
{
    /// <summary>Not active this period: earns retail commission only.</summary>
    Member = 0,
    Associate = 1,
    Builder = 2,
    Leader = 3,
    Director = 4,
}

public record RankRule(Rank Rank, decimal GroupVolume, int ActiveFrontline, int PaidLevels);

/// <summary>
/// The compensation plan's settings. The defaults are built around three fairness rules:
/// pay comes from retail sales to real customers, never from recruiting; a member's own
/// purchases can't buy their way to "active" (no inventory loading); and total payout is
/// capped as a share of revenue so the plan stays solvent and predictable.
/// </summary>
public class CompensationSettings
{
    /// <summary>Retail commission on customer volume a member sells personally.</summary>
    public decimal RetailRate { get; init; } = 0.20m;

    /// <summary>Override rate per compressed level, level 1 first. Paid only on downline customer volume.</summary>
    public decimal[] OverrideRates { get; init; } = [0.05m, 0.04m, 0.03m, 0.02m, 0.01m];

    /// <summary>Qualifying volume needed in the period to be active and earn overrides.</summary>
    public decimal ActiveThreshold { get; init; } = 100m;

    /// <summary>
    /// Personal-use volume counts toward activity only up to this multiple of customer volume.
    /// At 1.0 a member with no customers can never qualify by buying product themselves.
    /// </summary>
    public decimal PersonalUseMatch { get; init; } = 1.0m;

    /// <summary>No single frontline leg may supply more than this share of a rank's group-volume requirement.</summary>
    public decimal MaxLegShare { get; init; } = 0.5m;

    /// <summary>Total payout (retail + overrides) may not exceed this share of period revenue.</summary>
    public decimal PayoutCap { get; init; } = 0.40m;

    public RankRule[] Ranks { get; init; } =
    [
        new(Rank.Associate, 0m, 0, 2),
        new(Rank.Builder, 1_000m, 2, 3),
        new(Rank.Leader, 5_000m, 3, 4),
        new(Rank.Director, 20_000m, 4, 5),
    ];
}

public record PlanMember(int Id, int? SponsorId);

public record PlanOrder(int MemberId, BuyerType BuyerType, decimal Total, decimal Volume, OrderStatus Status = OrderStatus.Paid);

public record MemberResult(
    int MemberId,
    decimal CustomerVolume,
    decimal PersonalVolume,
    decimal QualifyingVolume,
    bool Active,
    decimal GroupVolume,
    int ActiveFrontline,
    Rank Rank,
    decimal Retail,
    decimal Overrides)
{
    public decimal Total => Retail + Overrides;
}

public record PayoutLine(int MemberId, CommissionType Type, decimal Amount, int Level, int? SourceMemberId);

public record PlanResult(
    decimal Revenue,
    decimal RetailTotal,
    decimal OverrideTotal,
    decimal OverrideScale,
    IReadOnlyDictionary<int, MemberResult> Members,
    IReadOnlyList<PayoutLine> Lines)
{
    public decimal TotalPayout => RetailTotal + OverrideTotal;
    public decimal PayoutRatio => Revenue == 0 ? 0 : TotalPayout / Revenue;
}

/// <summary>Pure calculation of one period's commissions. No database access, so it is easy to test and audit.</summary>
public class CompensationEngine(CompensationSettings settings)
{
    public CompensationSettings Settings { get; } = settings;

    public PlanResult Calculate(IEnumerable<PlanMember> members, IEnumerable<PlanOrder> orders)
    {
        var memberList = members.ToList();
        var sponsorOf = memberList.ToDictionary(m => m.Id, m => m.SponsorId);
        var children = memberList
            .Where(m => m.SponsorId is int s && sponsorOf.ContainsKey(s))
            .GroupBy(m => m.SponsorId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToList());

        var paid = orders.Where(o => o.Status == OrderStatus.Paid && sponsorOf.ContainsKey(o.MemberId)).ToList();
        var revenue = paid.Sum(o => o.Total);

        var cv = memberList.ToDictionary(m => m.Id, _ => 0m);
        var pv = memberList.ToDictionary(m => m.Id, _ => 0m);
        foreach (var o in paid)
        {
            if (o.BuyerType == BuyerType.RetailCustomer) cv[o.MemberId] += o.Volume;
            else pv[o.MemberId] += o.Volume;
        }

        var qualifying = memberList.ToDictionary(
            m => m.Id,
            m => cv[m.Id] + Math.Min(pv[m.Id], cv[m.Id] * Settings.PersonalUseMatch));
        var active = qualifying.ToDictionary(kv => kv.Key, kv => kv.Value >= Settings.ActiveThreshold);

        // Group (customer) volume of each member's whole subtree, including their own.
        var subtreeCv = new Dictionary<int, decimal>();
        decimal Subtree(int id, HashSet<int> path)
        {
            if (subtreeCv.TryGetValue(id, out var v)) return v;
            if (!path.Add(id)) return 0m; // defensive: ignore sponsor cycles
            v = cv[id] + (children.TryGetValue(id, out var kids) ? kids.Sum(k => Subtree(k, path)) : 0m);
            path.Remove(id);
            return subtreeCv[id] = v;
        }
        foreach (var m in memberList) Subtree(m.Id, []);

        var rankOf = new Dictionary<int, Rank>();
        var gvOf = new Dictionary<int, decimal>();
        var activeFrontlineOf = new Dictionary<int, int>();
        foreach (var m in memberList)
        {
            var kids = children.GetValueOrDefault(m.Id) ?? [];
            var activeKids = kids.Count(k => active[k]);
            activeFrontlineOf[m.Id] = activeKids;
            gvOf[m.Id] = subtreeCv[m.Id];

            var rank = Rank.Member;
            if (active[m.Id])
            {
                foreach (var rule in Settings.Ranks.OrderBy(r => r.Rank))
                {
                    var legCap = rule.GroupVolume * Settings.MaxLegShare;
                    var cappedGv = cv[m.Id] + kids.Sum(k => rule.GroupVolume == 0 ? subtreeCv[k] : Math.Min(subtreeCv[k], legCap));
                    if (cappedGv >= rule.GroupVolume && activeKids >= rule.ActiveFrontline) rank = rule.Rank;
                }
            }
            rankOf[m.Id] = rank;
        }

        int PaidLevels(Rank r) => Settings.Ranks.FirstOrDefault(x => x.Rank == r)?.PaidLevels ?? 0;

        var lines = new List<PayoutLine>();
        var retail = memberList.ToDictionary(m => m.Id, _ => 0m);
        foreach (var m in memberList.Where(m => cv[m.Id] > 0))
        {
            var amount = Floor(cv[m.Id] * Settings.RetailRate);
            retail[m.Id] = amount;
            lines.Add(new PayoutLine(m.Id, CommissionType.Retail, amount, 0, null));
        }

        // Overrides with dynamic compression: inactive uplines are skipped and don't use up a level.
        var rawOverrides = new List<(int To, decimal Amount, int Level, int From)>();
        foreach (var source in memberList.Where(m => cv[m.Id] > 0))
        {
            var level = 0;
            var seen = new HashSet<int> { source.Id };
            var upline = sponsorOf[source.Id];
            while (upline is int up && level < Settings.OverrideRates.Length && seen.Add(up))
            {
                if (active[up])
                {
                    level++;
                    if (level <= PaidLevels(rankOf[up]))
                        rawOverrides.Add((up, cv[source.Id] * Settings.OverrideRates[level - 1], level, source.Id));
                }
                upline = sponsorOf.GetValueOrDefault(up);
            }
        }

        var retailTotal = retail.Values.Sum();
        var rawOverrideTotal = rawOverrides.Sum(o => o.Amount);
        var cap = revenue * Settings.PayoutCap;
        var scale = 1m;
        if (rawOverrideTotal > 0 && retailTotal + rawOverrideTotal > cap)
            scale = Math.Max(0m, (cap - retailTotal) / rawOverrideTotal);

        var overrides = memberList.ToDictionary(m => m.Id, _ => 0m);
        foreach (var o in rawOverrides)
        {
            var amount = Floor(o.Amount * scale);
            if (amount <= 0) continue;
            overrides[o.To] += amount;
            lines.Add(new PayoutLine(o.To, CommissionType.Override, amount, o.Level, o.From));
        }

        var results = memberList.ToDictionary(m => m.Id, m => new MemberResult(
            m.Id, cv[m.Id], pv[m.Id], qualifying[m.Id], active[m.Id], gvOf[m.Id],
            activeFrontlineOf[m.Id], rankOf[m.Id], retail[m.Id], overrides[m.Id]));

        return new PlanResult(revenue, retailTotal, overrides.Values.Sum(), Math.Round(scale, 4), results, lines);
    }

    private static decimal Floor(decimal amount) => Math.Floor(amount * 100m) / 100m;
}
