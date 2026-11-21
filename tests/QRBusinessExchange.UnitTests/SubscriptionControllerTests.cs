using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using QRBusinessExchange.API.Controllers;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// Paid plans can't be bought online yet: only admins may switch an account to one, and anyone may go back
/// to Free.
/// </summary>
public class SubscriptionControllerTests
{
    private readonly FakeUsers users = new();
    private readonly FakeSubscriptions subscriptions = new();

    private SubscriptionController ControllerFor(User user)
    {
        users.Items[user.UserId] = user;
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user.UserId.ToString())], "test"))
        };
        return new SubscriptionController(subscriptions, users, NullLogger<SubscriptionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static User NewUser(string role = "User") => new()
    {
        UserId = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@example.com", FirstName = "Test", LastName = "User", Role = role
    };

    private Subscription ActiveSubscription(Guid userId, string type)
    {
        var s = new Subscription
        {
            SubscriptionId = Guid.NewGuid(), UserId = userId, SubscriptionType = type, CreditsAvailable = 50,
            IsActive = true, AutoRenew = true, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1)
        };
        subscriptions.Items.Add(s);
        return s;
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Premium")]
    [InlineData("Enterprise")]
    public async Task Ordinary_user_cannot_give_themselves_a_paid_plan(string plan)
    {
        var user = NewUser();
        var current = ActiveSubscription(user.UserId, "Free");

        var result = await ControllerFor(user).UpgradeSubscription(plan);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.True(current.IsActive);
        Assert.Single(subscriptions.Items);
    }

    [Fact]
    public async Task Admin_can_switch_their_account_to_a_paid_plan()
    {
        var admin = NewUser("Admin");
        ActiveSubscription(admin.UserId, "Free");

        var result = await ControllerFor(admin).UpgradeSubscription("Premium");

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(subscriptions.Items, s => s.IsActive && s.SubscriptionType == "Premium");
    }

    [Fact]
    public async Task Switching_to_free_ends_every_active_subscription()
    {
        var user = NewUser();
        var paid = ActiveSubscription(user.UserId, "Premium");
        var other = ActiveSubscription(Guid.NewGuid(), "Basic");

        var result = await ControllerFor(user).DowngradeToFree();

        Assert.IsType<NoContentResult>(result);
        Assert.False(paid.IsActive);
        Assert.False(paid.AutoRenew);
        Assert.True(other.IsActive); // someone else's plan is untouched
    }

    private sealed class FakeUsers : IUserRepository
    {
        public Dictionary<Guid, User> Items { get; } = new();
        public Task<User?> GetByIdAsync(Guid userId) => Task.FromResult(Items.GetValueOrDefault(userId));
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(Items.Values.FirstOrDefault(u => u.Email == email));
        public Task<User> CreateAsync(User user) { Items[user.UserId] = user; return Task.FromResult(user); }
        public Task<User> UpdateAsync(User user) { Items[user.UserId] = user; return Task.FromResult(user); }
        public Task<bool> DeleteAsync(Guid userId) => Task.FromResult(Items.Remove(userId));
    }

    private sealed class FakeSubscriptions : ISubscriptionRepository
    {
        public List<Subscription> Items { get; } = new();
        public Task<Subscription?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(s => s.SubscriptionId == id));
        public Task<IEnumerable<Subscription>> GetByUserIdAsync(Guid userId) => Task.FromResult(Items.Where(s => s.UserId == userId));
        public Task<Subscription?> GetActiveSubscriptionAsync(Guid userId) =>
            Task.FromResult(Items.FirstOrDefault(s => s.UserId == userId && s.IsActive));
        public Task<Subscription> CreateAsync(Subscription s) { Items.Add(s); return Task.FromResult(s); }
        public Task<Subscription> UpdateAsync(Subscription s) => Task.FromResult(s);
    }
}
