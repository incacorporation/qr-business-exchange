using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Qrbound.SignIn;

namespace Microsoft.Extensions.DependencyInjection;

public static class QrboundServiceCollectionExtensions
{
    /// <summary>
    /// Registers the qrbound sign-in components and <see cref="QrboundClient"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets the qrbound URL, client_id and redirect URI.</param>
    /// <param name="useAuthenticationStateProvider">
    /// Also register <see cref="QrboundAuthenticationStateProvider"/> as the app's
    /// <see cref="AuthenticationStateProvider"/>. Leave false when your app has its own (e.g. a cookie on the server).
    /// </param>
    public static IServiceCollection AddQrboundSignIn(this IServiceCollection services,
        Action<QrboundOptions> configure, bool useAuthenticationStateProvider = false)
    {
        services.AddOptions<QrboundOptions>()
            .Configure(configure)
            .Validate(o => { o.Validate(); return true; });

        services.AddHttpClient(QrboundClient.HttpClientName, (sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<QrboundOptions>>().Value;
            http.BaseAddress = new Uri(options.AuthorityTrimmed + "/");
        });
        services.AddHttpClient(QrboundClient.HttpClientName + ".Backend");

        services.TryAddScoped<IQrboundStateStore, SessionStorageQrboundStateStore>();
        services.TryAddScoped<QrboundAuthState>();
        services.TryAddScoped<QrboundClient>();

        if (useAuthenticationStateProvider)
        {
            services.AddAuthorizationCore();
            services.AddCascadingAuthenticationState();
            services.AddScoped<QrboundAuthenticationStateProvider>();
            services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<QrboundAuthenticationStateProvider>());
        }

        return services;
    }
}
