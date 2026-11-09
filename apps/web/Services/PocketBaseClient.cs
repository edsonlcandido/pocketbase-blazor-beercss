using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Web.Services;

/// <summary>Erro devolvido pela API do PocketBase.</summary>
public sealed class PocketBaseException : Exception
{
    public PocketBaseException(int statusCode, string message, IReadOnlyDictionary<string, string>? fieldErrors = null)
        : base(message)
    {
        StatusCode = statusCode;
        FieldErrors = fieldErrors ?? new Dictionary<string, string>();
    }

    public int StatusCode { get; }

    /// <summary>Mensagem por campo, no formato <c>data.email.message</c> do PocketBase.</summary>
    public IReadOnlyDictionary<string, string> FieldErrors { get; }
}

/// <summary>
/// Cliente HTTP do PocketBase — sem dependência externa.
///
/// Cobre o que o template precisa: autenticação, CRUD do registro `users` e
/// upload de arquivo. A superfície da API REST do PocketBase é estável, então
/// manter o cliente aqui evita acoplar o projeto à maturidade de um SDK
/// comunitário (que ainda está em 0.x) e deixa o template durar mais.
/// </summary>
public sealed class PocketBaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly PocketBaseOptions _options;
    private readonly NavigationManager _navigation;

    public PocketBaseClient(HttpClient http, PocketBaseOptions options, NavigationManager navigation)
    {
        _http = http;
        _options = options;
        _navigation = navigation;
    }

    /// <summary>Token da sessão atual (null quando deslogado).</summary>
    public string? Token { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    /// <summary>URL base absoluta do PocketBase, resolvida a partir da origem do app.</summary>
    public string BaseUrl
    {
        get
        {
            var configured = _options.Url.Trim();
            if (configured.Length == 0)
            {
                configured = "/";
            }

            if (Uri.TryCreate(configured, UriKind.Absolute, out var absolute))
            {
                return (absolute.GetLeftPart(UriPartial.Authority) + absolute.AbsolutePath).TrimEnd('/');
            }

            // URL relativa: mesma origem do app (produção).
            var origin = new Uri(_navigation.BaseUri).GetLeftPart(UriPartial.Authority);
            return (origin + configured).TrimEnd('/');
        }
    }

    public void SetToken(string? token) => Token = token;

    // ---------------------------------------------------------------- auth

    public async Task<PbAuthResponse> AuthWithPasswordAsync(string identity, string password, CancellationToken ct = default)
    {
        var payload = new { identity, password };
        var result = await SendAsync<PbAuthResponse>(
            HttpMethod.Post, "/api/collections/users/auth-with-password", payload, ct);

        Token = result.Token;
        return result;
    }

    public async Task<PbUser> AuthRefreshAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<PbAuthResponse>(
            HttpMethod.Post, "/api/collections/users/auth-refresh", null, ct);

        Token = result.Token;
        return result.Record;
    }

    public async Task<PbUser> CreateUserAsync(
        string email, string password, string passwordConfirm, string name, CancellationToken ct = default)
    {
        var payload = new { email, password, passwordConfirm, name };
        return await SendAsync<PbUser>(HttpMethod.Post, "/api/collections/users/records", payload, ct);
    }

    // ------------------------------------------------------------- registros

    public async Task<PbUser> GetUserAsync(string id, CancellationToken ct = default)
        => await SendAsync<PbUser>(HttpMethod.Get, $"/api/collections/users/records/{id}", null, ct);

    public async Task<PbUser> UpdateUserAsync(string id, object data, CancellationToken ct = default)
        => await SendAsync<PbUser>(HttpMethod.Patch, $"/api/collections/users/records/{id}", data, ct);

    /// <summary>Envia um arquivo para um campo <c>file</c> do registro.</summary>
    public async Task<PbUser> UploadFileAsync(
        string id,
        string field,
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        using var multipart = new MultipartFormDataContent();

        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        multipart.Add(fileContent, field, fileName);

        return await SendAsync<PbUser>(HttpMethod.Patch, $"/api/collections/users/records/{id}", multipart, ct);
    }

    /// <summary>URL pública de um arquivo do registro.</summary>
    public string GetFileUrl(PbUser user, string field = "avatar")
    {
        var fileName = field == "avatar" ? user.Avatar : string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return $"{BaseUrl}/api/files/{user.CollectionId}/{user.Id}/{fileName}";
    }

    // ------------------------------------------------------------- plumbing

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, BaseUrl + path);

        if (!string.IsNullOrEmpty(Token))
        {
            // PocketBase 0.40 espera o token CRU no header Authorization
            // (sem o prefixo "Token "). É o mesmo formato do SDK JS.
            request.Headers.TryAddWithoutValidation("Authorization", Token);
        }

        switch (body)
        {
            case MultipartFormDataContent multipart:
                request.Content = multipart;
                break;
            case not null:
                var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                break;
        }

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildExceptionAsync(response, ct);
        }

        var payload = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions) ?? default!;
    }

    private static async Task<PocketBaseException> BuildExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var message = response.ReasonPhrase ?? "Não foi possível concluir a requisição.";
        var fieldErrors = new Dictionary<string, string>();

        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var document = JsonDocument.Parse(raw);
                var root = document.RootElement;

                if (root.TryGetProperty("message", out var messageProperty) &&
                    messageProperty.ValueKind == JsonValueKind.String)
                {
                    message = messageProperty.GetString() ?? message;
                }

                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in data.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Object &&
                            property.Value.TryGetProperty("message", out var fieldMessage) &&
                            fieldMessage.ValueKind == JsonValueKind.String)
                        {
                            fieldErrors[property.Name] = fieldMessage.GetString() ?? string.Empty;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Corpo não é JSON (ex.: proxy devolveu HTML) — mantém o ReasonPhrase.
        }

        return new PocketBaseException(status, message, fieldErrors);
    }
}
