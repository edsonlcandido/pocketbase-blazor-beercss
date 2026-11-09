namespace Landing;

/// <summary>Configuração da landing page.</summary>
public sealed class LandingOptions
{
    public LandingOptions(string webAppUrl) => WebAppUrl = webAppUrl;

    /// <summary>
    /// URL do web app (SPA logada).
    /// <c>http://localhost:5174</c> em dev, <c>/app</c> em produção.
    /// </summary>
    public string WebAppUrl { get; }
}
