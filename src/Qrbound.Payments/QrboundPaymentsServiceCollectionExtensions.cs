using Qrbound.Payments;

namespace Microsoft.Extensions.DependencyInjection;

public static class QrboundPaymentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="QrboundPaymentsClient"/> as a typed HttpClient.
    /// Chain your own handlers on the returned builder, for example one that adds the user's bearer token.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiBaseAddress">Base address of the API that hosts the payment endpoints.</param>
    public static IHttpClientBuilder AddQrboundPayments(this IServiceCollection services, Uri apiBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(apiBaseAddress);

        return services.AddHttpClient<QrboundPaymentsClient>(http => http.BaseAddress = apiBaseAddress);
    }
}
