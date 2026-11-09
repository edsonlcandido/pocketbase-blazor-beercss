using System.Text.Json.Serialization;

namespace Web.Services;

/// <summary>Registro de usuário devolvido pelo PocketBase.</summary>
public sealed class PbUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("collectionId")]
    public string CollectionId { get; set; } = string.Empty;

    [JsonPropertyName("collectionName")]
    public string CollectionName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    /// <summary>Campo customizado do template (criado pela migration inicial).</summary>
    [JsonPropertyName("emailVisibility")]
    public bool EmailVisibility { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;

    [JsonPropertyName("updated")]
    public string Updated { get; set; } = string.Empty;
}

/// <summary>Resposta de autenticação do PocketBase (token + record).</summary>
public sealed class PbAuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("record")]
    public PbUser Record { get; set; } = new();
}
