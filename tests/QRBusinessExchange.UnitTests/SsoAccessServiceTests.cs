using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// Application categories: one user is a "business" on one site and a "client" on another;
/// categories that need approval wait for the app's Owners/Admins, who are emailed, and so is the user.
/// Runs on EF Core's in-memory provider (never the shared database).
/// </summary>
public class SsoAccessServiceTests
{
    private readonly ApplicationDbContext _db;
    private readonly RecordingEmail _email = new();
    private readonly SsoAccessService _service;

    // eatinhouston: owned by Ana; John owns "John's Tacos" and wants to sign in for it
    private readonly User _ana, _john, _mary;
    private readonly Business _eatin, _tacos;
    private readonly SsoApplication _eatinApp, _sampleApp;
    private readonly SsoAppCategory _business, _client, _vip;

    public SsoAccessServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        _ana = AddUser("ana@eatin.test", "Ana");
        _john = AddUser("john.doe@example.test", "John");
        _mary = AddUser("mary@example.test", "Mary");

        _eatin = AddBusiness("Eat in Houston", "eatinhouston", _ana, BusinessRoles.Owner);
        _tacos = AddBusiness("John's Tacos", "johns-tacos", _john, BusinessRoles.Owner);
        var sampleOwner = AddBusiness("Sample Co", "sample", _mary, BusinessRoles.Owner);

        _eatinApp = AddApp(_eatin, "www.eatinhouston.com", "qrb_eatin", "https://www.eatinhouston.com/qrbound/callback");
        _sampleApp = AddApp(sampleOwner, "sample.qrbound.com", "qrb_sample", "https://sample.qrbound.com/qrbound/callback");

        _business = AddCategory(_eatinApp, "business", "Restaurant", requiresApproval: true, requiresBusiness: true, order: 0);
        _vip = AddCategory(_eatinApp, "vip", "VIP guest", requiresApproval: true, order: 5);
        _client = AddCategory(_sampleApp, "client", "Client", requiresApproval: false, isDefault: true, order: 0);
        _db.SaveChanges();

        _service = new SsoAccessService(_db, _email,
            Options.Create(new SsoSettings { PortalBaseUrl = "https://portal.test" }),
            NullLogger<SsoAccessService>.Instance);
    }

    [Fact]
    public async Task Same_user_is_a_business_on_one_site_and_a_client_on_another()
    {
        // John requests "business" for John's Tacos on eatinhouston; Ana approves
        await _service.RequestAsync(_john.UserId, new SsoAccessRequest
        {
            ClientId = "qrb_eatin", Category = "business", BusinessId = _tacos.BusinessId, Note = "We serve tacos"
        });
        var request = (await _service.GetAccessAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, "Pending")).Single();
        await _service.DecideAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, request.SsoAppAccessId, SsoAccessStatus.Approved, null);

        await _service.EnsureCanSignInAsync(_eatinApp, _john.UserId, "business");
        var (eatinCategories, actingFor) = await _service.GetClaimsAsync(_eatinApp, _john.UserId);
        Assert.Equal(new[] { "business" }, eatinCategories);
        Assert.Equal(_tacos.BusinessId, actingFor?.BusinessId);

        // On the sample site he is simply a client (default category, granted on sign-in)
        await _service.EnsureCanSignInAsync(_sampleApp, _john.UserId, null);
        var (sampleCategories, sampleOrg) = await _service.GetClaimsAsync(_sampleApp, _john.UserId);
        Assert.Equal(new[] { "client" }, sampleCategories);
        Assert.Null(sampleOrg);
    }

    [Fact]
    public async Task A_request_needing_approval_emails_the_app_admins_and_blocks_sign_in_until_approved()
    {
        var result = await _service.RequestAsync(_john.UserId, new SsoAccessRequest
        {
            ClientId = "qrb_eatin", Category = "business", BusinessId = _tacos.BusinessId
        });

        Assert.Equal(SsoAccessStatus.Pending, result.Status);
        var sent = Assert.Single(_email.Requested);
        Assert.Equal("ana@eatin.test", sent.To);
        Assert.Contains($"/businesses/{_eatin.BusinessId}/applications/{_eatinApp.SsoApplicationId}/access", sent.ReviewUrl);
        Assert.Equal("John's Tacos", sent.ActingBusiness);

        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _john.UserId, "business"));
        Assert.Equal(SsoAccessService.Errors.Pending, ex.Error);

        // Asking again while pending doesn't email twice
        await _service.RequestAsync(_john.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "business", BusinessId = _tacos.BusinessId });
        Assert.Single(_email.Requested);
    }

    [Fact]
    public async Task The_user_is_emailed_the_decision_and_a_rejected_user_can_ask_again()
    {
        await _service.RequestAsync(_john.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "vip" });
        var access = (await _service.GetAccessAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, null)).Single();

        await _service.DecideAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, access.SsoAppAccessId,
            SsoAccessStatus.Rejected, "Members of our loyalty club only");

        var decision = Assert.Single(_email.Decisions);
        Assert.Equal(("john.doe@example.test", "Rejected", "Members of our loyalty club only", "https://www.eatinhouston.com"),
            (decision.To, decision.Status, decision.Note, decision.AppUrl));

        var denied = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _john.UserId, "vip"));
        Assert.Equal(SsoAccessService.Errors.Denied, denied.Error);
        Assert.Contains("loyalty club", denied.Message);

        var again = await _service.RequestAsync(_john.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "vip" });
        Assert.Equal(SsoAccessStatus.Pending, again.Status);
        Assert.Equal(2, _email.Requested.Count);
    }

    [Fact]
    public async Task Only_owners_and_admins_of_the_apps_business_can_decide()
    {
        await _service.RequestAsync(_john.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "vip" });
        var access = (await _service.GetAccessAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, null)).Single();

        // John isn't a member of Eat in Houston at all
        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.DecideAsync(
            _john.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, access.SsoAppAccessId, SsoAccessStatus.Approved, null));
        Assert.Equal(404, ex.StatusCode);

        // A plain member may look but not decide
        _db.BusinessMembers.Add(new BusinessMember { BusinessId = _eatin.BusinessId, UserId = _mary.UserId, Role = BusinessRoles.Member });
        await _db.SaveChangesAsync();
        Assert.Single(await _service.GetAccessAsync(_mary.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, null));
        var forbidden = await Assert.ThrowsAsync<SsoException>(() => _service.DecideAsync(
            _mary.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, access.SsoAppAccessId, SsoAccessStatus.Approved, null));
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task Acting_for_a_business_requires_being_its_owner_or_admin()
    {
        // Mary tries to sign in to eatinhouston for John's Tacos
        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.RequestAsync(_mary.UserId, new SsoAccessRequest
        {
            ClientId = "qrb_eatin", Category = "business", BusinessId = _tacos.BusinessId
        }));
        Assert.Equal("invalid_request", ex.Error);

        var noBusiness = await Assert.ThrowsAsync<SsoException>(() => _service.RequestAsync(_john.UserId,
            new SsoAccessRequest { ClientId = "qrb_eatin", Category = "business" }));
        Assert.Equal("invalid_request", noBusiness.Error);
    }

    [Fact]
    public async Task Approval_for_a_business_stops_counting_when_the_user_no_longer_manages_it()
    {
        await _service.RequestAsync(_john.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "business", BusinessId = _tacos.BusinessId });
        var access = (await _service.GetAccessAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, null)).Single();
        await _service.DecideAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, access.SsoAppAccessId, SsoAccessStatus.Approved, null);

        var membership = await _db.BusinessMembers.SingleAsync(m => m.BusinessId == _tacos.BusinessId && m.UserId == _john.UserId);
        membership.Role = BusinessRoles.Member;
        await _db.SaveChangesAsync();

        var (categories, org) = await _service.GetClaimsAsync(_eatinApp, _john.UserId);
        Assert.Empty(categories);
        Assert.Null(org);
        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _john.UserId, "business"));
        Assert.Equal(SsoAccessService.Errors.Required, ex.Error);
    }

    [Fact]
    public async Task Revoked_access_blocks_sign_in()
    {
        await _service.RequestAsync(_john.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "vip" });
        var id = (await _service.GetAccessAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, null)).Single().SsoAppAccessId;
        await _service.DecideAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, id, SsoAccessStatus.Approved, null);
        await _service.EnsureCanSignInAsync(_eatinApp, _john.UserId, "vip");

        await _service.DecideAsync(_ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, id, SsoAccessStatus.Revoked, null);

        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _john.UserId, "vip"));
        Assert.Equal(SsoAccessService.Errors.Denied, ex.Error);
        Assert.Equal("Revoked", _email.Decisions.Last().Status);

        // A pending request can't be revoked, an approved one can't be rejected
        var invalid = await Assert.ThrowsAsync<SsoException>(() => _service.DecideAsync(
            _ana.UserId, _eatin.BusinessId, _eatinApp.SsoApplicationId, id, SsoAccessStatus.Rejected, null));
        Assert.Equal(409, invalid.StatusCode);
    }

    [Fact]
    public async Task Apps_that_require_an_approved_category_turn_away_users_without_one()
    {
        _eatinApp.RequireApprovedCategory = true;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _mary.UserId, null));
        Assert.Equal(SsoAccessService.Errors.Required, ex.Error);

        await _service.RequestAsync(_mary.UserId, new SsoAccessRequest { ClientId = "qrb_eatin", Category = "vip" });
        var pending = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _mary.UserId, null));
        Assert.Equal(SsoAccessService.Errors.Pending, pending.Error);
    }

    [Fact]
    public async Task Apps_without_categories_behave_as_before_and_reject_unknown_categories()
    {
        var plainApp = AddApp(_eatin, "plain", "qrb_plain", "https://plain.test/cb");
        await _db.SaveChangesAsync();

        await _service.EnsureCanSignInAsync(plainApp, _mary.UserId, null);
        Assert.Empty((await _service.GetClaimsAsync(plainApp, _mary.UserId)).Categories);

        var ex = await Assert.ThrowsAsync<SsoException>(() => _service.EnsureCanSignInAsync(_eatinApp, _mary.UserId, "superuser"));
        Assert.Equal("invalid_request", ex.Error);
    }

    [Fact]
    public async Task Category_access_lists_the_businesses_the_user_may_act_for()
    {
        _db.BusinessMembers.Add(new BusinessMember { BusinessId = _eatin.BusinessId, UserId = _john.UserId, Role = BusinessRoles.Member });
        await _db.SaveChangesAsync();

        var access = await _service.GetCategoryAccessAsync(_john.UserId, "qrb_eatin", "business");

        Assert.Null(access.Status);
        Assert.True(access.RequiresBusiness);
        var only = Assert.Single(access.EligibleBusinesses); // a plain Member of Eat in Houston can't act for it
        Assert.Equal("John's Tacos", only.Name);
    }

    [Fact]
    public async Task Default_categories_must_not_need_approval_and_keys_are_unique()
    {
        var bad = await Assert.ThrowsAsync<SsoException>(() => _service.SaveCategoryAsync(_ana.UserId, _eatin.BusinessId,
            _eatinApp.SsoApplicationId, null, new SaveSsoAppCategoryRequest { Key = "guest", Name = "Guest", IsDefault = true, RequiresApproval = true }));
        Assert.Equal("invalid_request", bad.Error);

        var duplicate = await Assert.ThrowsAsync<SsoException>(() => _service.SaveCategoryAsync(_ana.UserId, _eatin.BusinessId,
            _eatinApp.SsoApplicationId, null, new SaveSsoAppCategoryRequest { Key = "VIP", Name = "Another VIP" }));
        Assert.Equal("duplicate_category", duplicate.Error);
    }

    // -----------------------------------------------------------------------

    private User AddUser(string email, string firstName)
    {
        var user = new User { Email = email, FirstName = firstName, LastName = "Test", PasswordHash = "x", IsActive = true };
        _db.Users.Add(user);
        return user;
    }

    private Business AddBusiness(string name, string slug, User owner, string role)
    {
        var business = new Business { Name = name, Slug = slug, OwnerUserId = owner.UserId, IsActive = true };
        _db.Businesses.Add(business);
        _db.BusinessMembers.Add(new BusinessMember { BusinessId = business.BusinessId, UserId = owner.UserId, Role = role });
        return business;
    }

    private SsoApplication AddApp(Business business, string name, string clientId, string redirectUri)
    {
        var app = new SsoApplication { BusinessId = business.BusinessId, Business = business, Name = name, ClientId = clientId, RedirectUris = redirectUri };
        _db.SsoApplications.Add(app);
        return app;
    }

    private SsoAppCategory AddCategory(SsoApplication app, string key, string name, bool requiresApproval,
        bool requiresBusiness = false, bool isDefault = false, int order = 0)
    {
        var category = new SsoAppCategory
        {
            SsoApplicationId = app.SsoApplicationId, Key = key, Name = name, RequiresApproval = requiresApproval,
            RequiresBusiness = requiresBusiness, IsDefault = isDefault, SortOrder = order
        };
        _db.SsoAppCategories.Add(category);
        return category;
    }

    private sealed class RecordingEmail : IEmailService
    {
        public List<(string To, string ReviewUrl, string? ActingBusiness)> Requested { get; } = new();
        public List<(string To, string Status, string? Note, string? AppUrl)> Decisions { get; } = new();

        public Task<bool> SendSsoAccessRequestedEmailAsync(string email, string firstName, string requesterName,
            string requesterEmail, string applicationName, string categoryName, string? actingBusinessName,
            string? note, string reviewUrl)
        {
            Requested.Add((email, reviewUrl, actingBusinessName));
            return Task.FromResult(true);
        }

        public Task<bool> SendSsoAccessDecisionEmailAsync(string email, string firstName, string applicationName,
            string categoryName, string status, string? note, string? applicationUrl)
        {
            Decisions.Add((email, status, note, applicationUrl));
            return Task.FromResult(true);
        }

        public Task<bool> SendVerificationEmailAsync(Guid userId, string email, string verificationToken) => Task.FromResult(true);
        public Task<bool> SendConnectionNotificationAsync(Guid recipientUserId, string senderName, string? message = null) => Task.FromResult(true);
        public Task<bool> SendPasswordResetEmailAsync(string email, string firstName, string resetUrl, TimeSpan validFor) => Task.FromResult(true);
        public Task<bool> SendPasswordChangedEmailAsync(string email, string firstName, string forgotPasswordUrl) => Task.FromResult(true);
    }
}
