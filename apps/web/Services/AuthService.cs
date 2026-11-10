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

    private Task? _initialization;

    /// <summary>
    /// Restaura a sessão a partir do token salvo no navegador.
    /// Equivale ao <c>verifyAuth()</c> da store Pinia.
    ///
    /// Idempotente e segura para chamadas concorrentes: a primeira chamada
    /// dispara a restauração e todas as outras esperam a MESMA Task. Isso é o
    /// que permite ao <see cref="PocketBaseAuthenticationStateProvider"/>
    /// segurar o <c>AuthorizeRouteView</c> enquanto o token é lido — sem isso o
    /// guard avalia "anônimo" e joga a rota protegida para /login mesmo com a
    /// sessão válida (acontecia em todo F5).
    /// </summary>
    public Task InitializeAsync() => _initialization ??= RestoreSessionAsync();

    private async Task RestoreSessionAsync()
    {
        try
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
            catch (Exception exception)
            {
                // Token inválido/expirado, PocketBase fora do ar ou URL mal configurada:
                // limpa a sessão e segue para o login. O boot do app NÃO pode quebrar
                // por causa de uma sessão salva no navegador.
                Console.WriteLine($"[auth] falha ao restaurar a sessão: {exception.Message}");
                await LogoutAsync();
            }
        }
        catch (Exception exception)
        {
            // Nem a leitura do localStorage pode derrubar o boot do app.
            Console.WriteLine($"[auth] falha ao inicializar a sessão: {exception.Message}");
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
