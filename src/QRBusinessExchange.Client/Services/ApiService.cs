using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace QRBusinessExchange.Client.Services;

public class ApiService
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IndexedDBService _storage;

    public ApiService(HttpClient httpClient, IndexedDBService storage)
    {
        _httpClient = httpClient;
        _storage = storage;
    }

    /// <summary>
    /// Attaches the portal session token (if any) to outgoing requests.
    /// </summary>
    private async Task ApplyAuthAsync()
    {
        var token = await _storage.GetItemAsync<string>("authToken");
        _httpClient.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data)
    {
        await ApplyAuthAsync();
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PostAsync(endpoint, content);
    }

    public async Task<T?> ParseResponseAsync<T>(HttpResponseMessage response)
    {
        var jsonString = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(jsonString, ReadOptions);
    }

    /// <summary>
    /// Reads "error_description" / "error" from an API error response.
    /// </summary>
    public async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback = "Something went wrong")
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            foreach (var name in new[] { "error_description", "errorDescription", "error", "message" })
            {
                if (doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    // "error" is often a machine code such as "invalid_token"; only show it if it reads as a sentence.
                    if (!string.IsNullOrWhiteSpace(text) && (name != "error" || text.Contains(' ')))
                    {
                        return text;
                    }
                }
            }
        }
        catch
        {
            // fall through
        }

        return fallback;
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            await ApplyAuthAsync();
            return await _httpClient.GetFromJsonAsync<T>(endpoint, ReadOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// GET that returns the raw response so callers can inspect status codes.
    /// </summary>
    public async Task<HttpResponseMessage> GetResponseAsync(string endpoint)
    {
        await ApplyAuthAsync();
        return await _httpClient.GetAsync(endpoint);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data)
    {
        await ApplyAuthAsync();
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _httpClient.PutAsync(endpoint, content);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        await ApplyAuthAsync();
        return await _httpClient.DeleteAsync(endpoint);
    }

    /// <summary>Uploads one file as multipart/form-data (field name "file").</summary>
    public async Task<HttpResponseMessage> PostFileAsync(string endpoint, byte[] data, string fileName, string contentType)
    {
        await ApplyAuthAsync();
        using var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var form = new MultipartFormDataContent { { file, "file", fileName } };
        return await _httpClient.PostAsync(endpoint, form);
    }
}
