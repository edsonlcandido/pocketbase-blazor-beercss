using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Web;
using Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// URL do PocketBase: "/" (mesma origem) em produção, http://localhost:8090 em dev.
builder.Services.AddSingleton(new PocketBaseOptions(
    builder.Configuration["PocketBase:Url"] ?? "/"));

// O HttpClient é registrado aqui porque o PocketBaseClient depende dele.
// O BaseAddress é a ORIGEM do host (sem o path /app/), e o PocketBaseClient
// sempre monta URLs absolutas a partir do PocketBaseOptions.
var appBase = builder.HostEnvironment.BaseAddress;
var hostOrigin = new Uri(appBase);
if (string.IsNullOrEmpty(hostOrigin.Host) ||
    (hostOrigin.Scheme != Uri.UriSchemeHttp && hostOrigin.Scheme != Uri.UriSchemeHttps))
{
    // fallback: usa a URL do app como está (só acontece em origens não-http).
    hostOrigin = new Uri(appBase);
}
else
{
    hostOrigin = new Uri(hostOrigin.GetLeftPart(UriPartial.Authority));
}

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = hostOrigin });

builder.Services.AddScoped<PocketBaseClient>();
builder.Services.AddScoped<BrowserStorage>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AuthenticationStateProvider, PocketBaseAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
