namespace Web.Services;

/// <summary>
/// Configuração de conexão com o PocketBase.
/// </summary>
public sealed class PocketBaseOptions
{
    public PocketBaseOptions(string url) => Url = url;

    /// <summary>
    /// URL base do PocketBase.
    /// <c>/</c> (ou qualquer caminho relativo) significa "mesma origem do app" —
    /// que é o padrão em produção, onde o PocketBase serve o app e a API juntos.
    /// Uma URL absoluta aponta para um PocketBase em outro domínio.
    /// </summary>
    public string Url { get; }
}
