using System.Net.Http;

namespace Web.Services;

/// <summary>Resultado de uma tentativa de login/cadastro.</summary>
public sealed record AuthResult(bool Success, string? Error = null, IReadOnlyDictionary<string, string>? FieldErrors = null)
{
    public static AuthResult Ok() => new(true);

    public static AuthResult Fail(string error, IReadOnlyDictionary<string, string>? fieldErrors = null)
        => new(false, error, fieldErrors);
}

/// <summary>
/// Estado de autenticação do app: login, cadastro, logout e restauração de sessão.
/// Substitui a store Pinia `auth` do template Vue.
/// </summary>
public sealed class AuthService
{
    private const string TokenStorageKey = "pocketbase_auth";

    private readonly PocketBaseClient _pb;
    private readonly BrowserStorage _storage;

    public AuthService(PocketBaseClient pb, BrowserStorage storage)
    {
        _pb = pb;
        _storage = storage;
    }

    public PbUser? User { get; private set; }

    public bool IsAuthenticated => User is not null;

    /// <summary>Disparado sempre que o usuário loga, desloga ou os dados mudam.</summary>
    public event Action? AuthStateChanged;

    /// <summary>
    /// Restaura a sessão a partir do token salvo no navegador.
    /// Equivale ao <c>verifyAuth()</c> da store Pinia.
    /// </summary>
    public async Task InitializeAsync()
    {
        var token = await _storage.GetAsync(TokenStorageKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        _pb.SetToken(token);

        try
        {
            User = await _pb.AuthRefreshAsync();
            await _storage.SetAsync(TokenStorageKey, _pb.Token ?? token);
            Notify();
        }
        catch (Exception exception) when (exception is PocketBaseException or HttpRequestException)
        {
            // Token inválido ou usuário removido — limpa a sessão.
            await LogoutAsync();
        }
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        try
        {
            var result = await _pb.AuthWithPasswordAsync(email, password);
            User = result.Record;
            await _storage.SetAsync(TokenStorageKey, _pb.Token ?? string.Empty);
            Notify();
            return AuthResult.Ok();
        }
        catch (PocketBaseException exception)
        {
            return AuthResult.Fail(exception.Message, exception.FieldErrors);
        }
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string passwordConfirm, string name)
    {
        try
        {
            await _pb.CreateUserAsync(email, password, passwordConfirm, name);
        }
        catch (PocketBaseException exception)
        {
            return AuthResult.Fail(exception.Message, exception.FieldErrors);
        }

        // PocketBase não devolve token no create — faz login em seguida.
        return await LoginAsync(email, password);
    }

    public async Task LogoutAsync()
    {
        _pb.SetToken(null);
        User = null;
        await _storage.RemoveAsync(TokenStorageKey);
        Notify();
    }

    /// <summary>Atualiza o usuário em memória após salvar o perfil.</summary>
    public void SetUser(PbUser user)
    {
        User = user;
        Notify();
    }

    private void Notify() => AuthStateChanged?.Invoke();
}
