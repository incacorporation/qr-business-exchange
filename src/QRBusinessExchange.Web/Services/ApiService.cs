using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace QRBusinessExchange.Web.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly IndexedDBService _indexedDB;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, IndexedDBService indexedDB, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _indexedDB = indexedDB;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.GetFromJsonAsync<T>(endpoint);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP GET error for endpoint: {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.PostAsJsonAsync(endpoint, data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP POST error for endpoint: {Endpoint}", endpoint);
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.PutAsJsonAsync(endpoint, data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP PUT error for endpoint: {Endpoint}", endpoint);
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }

    public async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.DeleteAsync(endpoint);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP DELETE error for endpoint: {Endpoint}", endpoint);
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }

    public async Task<HttpResponseMessage> PostAsync(string endpoint, HttpContent content)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.PostAsync(endpoint, content);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP POST error for endpoint: {Endpoint}", endpoint);
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await _indexedDB.GetItemAsync<string>("authToken");
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<TResponse?> ParseResponseAsync<TResponse>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error for response");
            return default;
        }
    }
}
