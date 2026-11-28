using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Qrbound.POS.Data;
using Qrbound.POS.Services;

namespace Qrbound.POS.Components.Staff;

/// <summary>A staff page about one restaurant: /pos/{RestaurantId}/...</summary>
public abstract class RestaurantPageBase : ComponentBase, IDisposable
{
    [Parameter] public Guid RestaurantId { get; set; }

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    [Inject] protected RestaurantService Restaurants { get; set; } = default!;
    [Inject] protected PosEvents Events { get; set; } = default!;
    [Inject] protected TimeProvider Clock { get; set; } = default!;

    protected Restaurant? Restaurant { get; private set; }
    protected bool Loaded { get; private set; }
    protected string? Error { get; set; }
    protected string? Notice { get; set; }
    protected ClaimsPrincipal User { get; private set; } = new();

    protected string StaffName => User.Identity?.Name ?? "staff";
    protected Guid StaffId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    protected bool IsManager => User.IsInRole(nameof(StaffRole.Manager)) || User.IsInRole(nameof(StaffRole.Owner));

    protected DateTime UtcNow => Clock.GetUtcNow().UtcDateTime;
    protected DateOnly Today => Restaurant is null ? DateOnly.FromDateTime(UtcNow) : RestaurantService.BusinessDate(Restaurant, UtcNow);

    /// <summary>Subscribe to live order changes of this restaurant (kitchen, orders, register).</summary>
    protected virtual bool LiveUpdates => false;

    protected override async Task OnParametersSetAsync()
    {
        if (AuthState is not null) User = (await AuthState).User;
        if (Restaurant?.Id != RestaurantId)
        {
            Restaurant = await Restaurants.GetAsync(RestaurantId);
            if (Restaurant is not null) await LoadAsync();
        }

        Loaded = true;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && LiveUpdates) Events.OrderChanged += OnOrderChangedInternal;
    }

    /// <summary>Loads the page's data once the restaurant is known.</summary>
    protected virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>An order of this restaurant changed somewhere (another screen, a customer, a payment).</summary>
    protected virtual Task OnOrderChangedAsync(OrderChanged change) => LoadAsync();

    protected async Task ReloadRestaurantAsync() => Restaurant = await Restaurants.GetAsync(RestaurantId);

    /// <summary>Runs a staff action and shows its error, if any.</summary>
    protected async Task Try(Func<Task> action, string? success = null)
    {
        Error = null;
        Notice = null;
        try
        {
            await action();
            Notice = success;
        }
        catch (PosException ex)
        {
            Error = ex.Message;
        }
        catch (HttpRequestException)
        {
            Error = "qrbound can't be reached right now. Try again in a moment.";
        }
    }

    private void OnOrderChangedInternal(OrderChanged change)
    {
        if (change.RestaurantId != RestaurantId) return;
        _ = InvokeAsync(async () =>
        {
            await OnOrderChangedAsync(change);
            StateHasChanged();
        });
    }

    public virtual void Dispose()
    {
        if (LiveUpdates) Events.OrderChanged -= OnOrderChangedInternal;
        GC.SuppressFinalize(this);
    }
}
