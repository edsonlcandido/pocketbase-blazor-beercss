using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Web.Services;

/// <summary>
/// Expõe o estado de autenticação do <see cref="AuthService"/> para o
/// <c>AuthorizeRouteView</c> do Blazor — o equivalente ao navigation guard
/// com <c>meta.requiresAuth</c> do Vue Router.
/// </summary>
public sealed class PocketBaseAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _auth;

    public PocketBaseAuthenticationStateProvider(AuthService auth)
    {
        _auth = auth;
        _auth.AuthStateChanged += OnAuthStateChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _auth.User;

        var principal = user is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name),
                    new Claim(ClaimTypes.Email, user.Email),
                },
                authenticationType: "pocketbase"));

        return Task.FromResult(new AuthenticationState(principal));
    }

    private void OnAuthStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
