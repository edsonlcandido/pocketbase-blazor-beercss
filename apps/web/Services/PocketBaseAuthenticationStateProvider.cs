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

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Aguarda a restauração da sessão ANTES de reportar o estado.
        // O AuthorizeRouteView usa este await para decidir entre renderizar a
        // rota, mostrar o <Authorizing> ou redirecionar para o login — sem a
        // espera, o guard avalia "anônimo" no primeiro render e joga a rota
        // protegida para /login mesmo com o token válido.
        await _auth.InitializeAsync();

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

        return new AuthenticationState(principal);
    }

    private void OnAuthStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
