using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.UnitTests;

public class CompensationEngineTests
{
    private static readonly CompensationEngine Engine = new(new CompensationSettings { PayoutCap = 1m });

    private static PlanOrder Retail(int member, decimal volume) => new(member, BuyerType.RetailCustomer, volume, volume);
    private static PlanOrder Personal(int member, decimal volume) => new(member, BuyerType.MemberPersonalUse, volume, volume);

    [Fact]
    public void Retail_commission_is_paid_on_customer_volume()
    {
        var result = Engine.Calculate([new(1, null)], [Retail(1, 150)]);

        Assert.Equal(30m, result.Members[1].Retail);
        Assert.True(result.Members[1].Active);
    }

    [Fact]
    public void Personal_purchases_alone_never_make_a_member_active()
    {
        var result = Engine.Calculate([new(1, null)], [Personal(1, 1_000)]);

        Assert.False(result.Members[1].Active);
        Assert.Equal(0m, result.Members[1].QualifyingVolume);
        Assert.Equal(Rank.Member, result.Members[1].Rank);
    }

    [Fact]
    public void Personal_purchases_count_only_up_to_customer_volume()
    {
        var result = Engine.Calculate([new(1, null)], [Retail(1, 60), Personal(1, 500)]);

        Assert.Equal(120m, result.Members[1].QualifyingVolume);
        Assert.True(result.Members[1].Active);
    }

    [Fact]
    public void Recruiting_without_sales_pays_nothing()
    {
        // Member 1 sponsors ten people who buy only for themselves.
        var members = new List<PlanMember> { new(1, null) };
        members.AddRange(Enumerable.Range(2, 10).Select(i => new PlanMember(i, 1)));
        var orders = Enumerable.Range(2, 10).Select(i => Personal(i, 200)).Append(Retail(1, 100)).ToList();

        var result = Engine.Calculate(members, orders);

        Assert.Equal(0m, result.Members[1].Overrides);
    }

    [Fact]
    public void Overrides_compress_past_inactive_uplines()
    {
        // 1 (active) -> 2 (inactive) -> 3 (sells 500)
        var result = Engine.Calculate(
            [new(1, null), new(2, 1), new(3, 2)],
            [Retail(1, 100), Retail(3, 500)]);

        var line = Assert.Single(result.Lines, l => l.Type == CommissionType.Override);
        Assert.Equal(1, line.MemberId);
        Assert.Equal(1, line.Level); // compressed: paid as level 1, not level 2
        Assert.Equal(25m, line.Amount); // 5% of 500
    }

    [Fact]
    public void Associates_are_paid_two_levels_deep_only()
    {
        // Chain 1 -> 2 -> 3 -> 4, everyone active; member 1 is an Associate (2 levels).
        var result = Engine.Calculate(
            [new(1, null), new(2, 1), new(3, 2), new(4, 3)],
            [Retail(1, 100), Retail(2, 100), Retail(3, 100), Retail(4, 100)]);

        Assert.Equal(Rank.Associate, result.Members[1].Rank);
        Assert.Equal([1, 2], result.Lines.Where(l => l.MemberId == 1 && l.Type == CommissionType.Override).Select(l => l.Level).Order());
    }

    [Fact]
    public void One_big_leg_cannot_carry_a_rank()
    {
        // Member 1 has two active frontline; one leg has 5,000 volume, the other 100. Builder needs 1,000
        // with at most 50% (500) from any one leg, so capped GV = 100 + 500 + 100 = 700.
        var result = Engine.Calculate(
            [new(1, null), new(2, 1), new(3, 1)],
            [Retail(1, 100), Retail(2, 5_000), Retail(3, 100)]);

        Assert.Equal(Rank.Associate, result.Members[1].Rank);
    }

    [Fact]
    public void Balanced_legs_reach_builder()
    {
        var result = Engine.Calculate(
            [new(1, null), new(2, 1), new(3, 1)],
            [Retail(1, 100), Retail(2, 500), Retail(3, 500)]);

        Assert.Equal(Rank.Builder, result.Members[1].Rank);
    }

    [Fact]
    public void Overrides_are_scaled_to_keep_payout_under_the_cap()
    {
        var engine = new CompensationEngine(new CompensationSettings { PayoutCap = 0.22m });
        var result = engine.Calculate(
            [new(1, null), new(2, 1)],
            [Retail(1, 1_000), Retail(2, 1_000)]);

        // Revenue 2,000; retail 400; raw override 50; cap 440 leaves 40 for overrides.
        Assert.Equal(0.8m, result.OverrideScale);
        Assert.Equal(40m, result.OverrideTotal);
        Assert.True(result.TotalPayout <= 440m);
    }

    [Fact]
    public void Refunded_orders_are_ignored()
    {
        var result = Engine.Calculate([new(1, null)],
            [new PlanOrder(1, BuyerType.RetailCustomer, 500, 500, OrderStatus.Refunded)]);

        Assert.Equal(0m, result.Revenue);
        Assert.Equal(0m, result.Members[1].Retail);
    }

    [Fact]
    public void Sponsor_cycles_do_not_hang()
    {
        var result = Engine.Calculate([new(1, 2), new(2, 1)], [Retail(1, 200), Retail(2, 200)]);
        Assert.Equal(2, result.Members.Count);
    }
}
