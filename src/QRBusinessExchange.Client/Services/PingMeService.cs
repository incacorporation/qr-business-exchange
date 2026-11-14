using QRBusinessExchange.Core.DTOs;

namespace QRBusinessExchange.Client.Services;

/// <summary>
/// Ping Me (be findable by people nearby) and the nearby search, shared by the Dashboard and Settings.
/// Turning Ping Me on shares this device's location; the switch's state comes from the server, so it's
/// the same on every device.
/// </summary>
public class PingMeService
{
    private readonly ApiService _api;
    private readonly GeolocationService _geolocation;
    private readonly StateService _state;

    public PingMeService(ApiService api, GeolocationService geolocation, StateService state)
    {
        _api = api;
        _geolocation = geolocation;
        _state = state;
    }

    /// <summary>Result of turning Ping Me on or off: the setting now in effect, and a problem to show if any.</summary>
    public sealed record ToggleResult(bool Enabled, string? Error);

    /// <summary>Loads the setting from the server into <see cref="StateService.IsPingMeEnabled"/>.</summary>
    public async Task<bool> LoadAsync()
    {
        try
        {
            var response = await _api.GetResponseAsync("api/location/ping-me");
            if (response.IsSuccessStatusCode && await _api.ParseResponseAsync<PingMeStatusDto>(response) is { } status)
            {
                _state.IsPingMeEnabled = status.Enabled;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Keep the last known value while offline.
        }
        return _state.IsPingMeEnabled;
    }

    /// <summary>Turns Ping Me on (sharing this device's location) or off.</summary>
    public async Task<ToggleResult> SetAsync(bool enabled)
    {
        try
        {
            HttpResponseMessage response;
            if (enabled)
            {
                var position = await _geolocation.GetCurrentPositionAsync();
                if (position == null)
                {
                    return new(_state.IsPingMeEnabled,
                        "Ping Me needs your location. Allow location access for qrbound in your browser, then try again.");
                }

                response = await _api.PostAsync("api/location/update", new ShareLocationRequest
                {
                    Latitude = position.Latitude,
                    Longitude = position.Longitude,
                    PingMeEnabled = true
                });
            }
            else
            {
                response = await _api.PutAsync("api/location/ping-me", new SetPingMeRequest { Enabled = false });
            }

            if (!response.IsSuccessStatusCode)
            {
                return new(_state.IsPingMeEnabled,
                    await _api.ReadErrorAsync(response, "Ping Me couldn't be changed. Please try again."));
            }

            var status = await _api.ParseResponseAsync<PingMeStatusDto>(response);
            _state.IsPingMeEnabled = status?.Enabled ?? enabled;
            return new(_state.IsPingMeEnabled, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(_state.IsPingMeEnabled, PortalAuthService.ConnectionProblem);
        }
    }

    /// <summary>
    /// Keeps the shared location current while Ping Me is on (called when the Dashboard gets a position).
    /// Does nothing when Ping Me is off, so no location is stored for people who haven't opted in.
    /// </summary>
    public async Task ShareIfEnabledAsync(double latitude, double longitude)
    {
        if (!_state.IsPingMeEnabled)
        {
            return;
        }

        try
        {
            await _api.PostAsync("api/location/update", new ShareLocationRequest { Latitude = latitude, Longitude = longitude });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
        }
    }

    /// <summary>People with Ping Me on within the radius, nearest first (empty if it can't be loaded).</summary>
    public async Task<(List<NearbyUserDto> People, string? Error)> GetNearbyAsync(double latitude, double longitude, double radiusMiles)
    {
        try
        {
            var url = FormattableString.Invariant(
                $"api/location/nearby?latitude={latitude}&longitude={longitude}&radiusMiles={radiusMiles}");
            var response = await _api.GetResponseAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return (new(), await _api.ReadErrorAsync(response, "Nearby people couldn't be loaded."));
            }
            return (await _api.ParseResponseAsync<List<NearbyUserDto>>(response) ?? new(), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (new(), PortalAuthService.ConnectionProblem);
        }
    }
}
