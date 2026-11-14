namespace QRBusinessExchange.Client.Services;

/// <summary>
/// The signed-in user's own picture as a data: URL, loaded once per picture version and shared by every
/// avatar on screen. It's fetched with the user's sign-in (api/user/profile-image), so it shows even when
/// the picture is hidden from their business card (the public image address only serves visible pictures).
/// </summary>
public class ProfilePictureCache
{
    private readonly ApiService _api;
    private string? _key;
    private Task<string?>? _load;

    public ProfilePictureCache(ApiService api)
    {
        _api = api;
    }

    /// <summary>
    /// The picture for the given ProfileImageUrl (it carries a version, so a new upload loads afresh);
    /// null when there is no picture or it can't be loaded.
    /// </summary>
    public Task<string?> GetAsync(string? profileImageUrl)
    {
        if (string.IsNullOrEmpty(profileImageUrl))
        {
            _key = null;
            _load = null;
            return Task.FromResult<string?>(null);
        }

        if (_key != profileImageUrl || _load == null)
        {
            _key = profileImageUrl;
            _load = LoadAsync();
        }
        return _load;
    }

    private async Task<string?> LoadAsync()
    {
        try
        {
            var response = await _api.GetResponseAsync("api/user/profile-image");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var type = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return $"data:{type};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _load = null; // try again next time instead of remembering the failure
            return null;
        }
    }
}
