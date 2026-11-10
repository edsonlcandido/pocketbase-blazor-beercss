---
name: blazor-service
description: Criar ou editar um serviço C# no app Blazor WebAssembly deste template — cliente do PocketBase, estado de autenticação, perfil, ou um serviço de domínio novo. Cobre o padrão de DI (scoped), o cliente HTTP fino do PocketBase (endpoints, header Authorization cru, erros), tipos de modelo e o que NÃO fazer (SDK externo, estado global, async void). Use quando o pedido mencionar "cria um serviço", "adiciona um endpoint no cliente PocketBase", "busca dados no backend", "estado global do app", "service no Blazor" ou qualquer mudança em apps/web/Services/.
---

# Blazor — Serviços (C#)

O app web (`apps/web`) usa DI do .NET. **Não existe store global**: o estado vive em serviços `scoped` registrados no `Program.cs`.

## Onde ficam

| Arquivo | Papel |
|---|---|
| `apps/web/Program.cs` | registro de DI |
| `apps/web/Services/PocketBaseClient.cs` | cliente HTTP do PocketBase |
| `apps/web/Services/PocketBaseModels.cs` | tipos de retorno da API |
| `apps/web/Services/AuthService.cs` | login/cadastro/logout/sessão |
| `apps/web/Services/ProfileService.cs` | dados do usuário logado |
| `apps/web/Services/BrowserStorage.cs` | `localStorage` via JS interop |

---

## Padrão: novo serviço

```csharp
// apps/web/Services/TodoService.cs
namespace Web.Services;

public sealed class TodoService
{
    private readonly PocketBaseClient _pb;

    public TodoService(PocketBaseClient pb) => _pb = pb;

    public async Task<List<Todo>> ListAsync(CancellationToken ct = default)
    {
        var response = await _pb.GetListAsync<Todo>("todos", ct);
        return response.Items;
    }
}
```

Registre no `Program.cs` (tudo `scoped`):

```csharp
builder.Services.AddScoped<TodoService>();
```

E use na página:

```razor
@inject TodoService Todos
```

**Regra:** serviço `scoped` = uma instância por "sessão do app" (no WASM, por app carregado). Singleton só para algo realmente imutável (ex.: `PocketBaseOptions`).

---

## Registro do `HttpClient` (obrigatório)

⚠️ **No Blazor WebAssembly o `HttpClient` NÃO é registrado por default.** Se um serviço pedir `HttpClient` no construtor e ele não estiver no DI, o app **não sobe** — a tela fica no loading e o console mostra:

```
ManagedError: CannotResolveService, System.Net.Http.HttpClient, Web.Services.PocketBaseClient
```

Isso aparece como "erro inesperado" do Blazor, não como erro de compilação.

E **`AddHttpClient()` não existe** neste projeto: a extensão vive em `Microsoft.Extensions.Http`, que o `Microsoft.AspNetCore.Components.WebAssembly` **não** traz junto. Usar ela dá `CS1061: 'IServiceCollection' não contém uma definição para "AddHttpClient"`.

A correção é registrar o `HttpClient` na mão:

```csharp
// apps/web/Program.cs
var appBase = builder.HostEnvironment.BaseAddress;
var hostOrigin = new Uri(appBase);
hostOrigin = string.IsNullOrEmpty(hostOrigin.Host)
    ? hostOrigin
    : new Uri(hostOrigin.GetLeftPart(UriPartial.Authority));

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = hostOrigin });
```

> O `BaseAddress` é a **origem** (sem o path `/app/`): assim uma URL relativa bate na API do PocketBase, que mora na raiz, e não dentro do app.

---

## O cliente do PocketBase

### Endpoints (PB 0.40)

| Operação | Método + caminho |
|---|---|
| Login | `POST /api/collections/users/auth-with-password` |
| Renovar token | `POST /api/collections/users/auth-refresh` |
| Criar registro | `POST /api/collections/{col}/records` |
| Ler registro | `GET /api/collections/{col}/records/{id}` |
| Atualizar | `PATCH /api/collections/{col}/records/{id}` |
| Deletar | `DELETE /api/collections/{col}/records/{id}` |
| Listar | `GET /api/collections/{col}/records?page=1&perPage=50` |
| Arquivo | `GET /api/files/{collectionId}/{recordId}/{fileName}` |

> ⚠️ **O path de registro tem `/records`**: `/api/collections/users` **não** funciona para CRUD (404). Só `auth-*` vai direto na collection.

### Header de autenticação — CRU

```csharp
// ✅ PocketBase 0.40
request.Headers.TryAddWithoutValidation("Authorization", Token);

// ❌ devolve 404 silencioso
request.Headers.Authorization = new AuthenticationHeaderValue("Token", Token);
```

É o mesmo formato do SDK JS: `Authorization: <token>`, **sem** o prefixo `Token `.

### Erros

O PB devolve `{ code, message, data: { campo: { message } } }`. O cliente já converte em `PocketBaseException`:

```csharp
try
{
    await Profile.UpdateNameAsync(nome);
}
catch (PocketBaseException ex)
{
    _error = ex.Message;                 // "Failed to update record."
    _erroNoCampo = ex.FieldErrors["name"]; // erro por campo, se houver
}
```

### Novo endpoint no cliente

Adicione em `PocketBaseClient.cs`, reaproveitando o `SendAsync<T>`:

```csharp
public async Task<PbList<Todo>> GetListAsync<T>(string collection, CancellationToken ct = default)
{
    return await SendAsync<PbList<Todo>>(
        HttpMethod.Get, $"/api/collections/{collection}/records?page=1&perPage=50", null, ct);
}
```

E o envelope de lista em `PocketBaseModels.cs`:

```csharp
public sealed class PbList<T>
{
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("perPage")] public int PerPage { get; set; }
    [JsonPropertyName("totalItems")] public int TotalItems { get; set; }
    [JsonPropertyName("totalPages")] public int TotalPages { get; set; }
    [JsonPropertyName("items")] public List<T> Items { get; set; } = new();
}
```

### URL base: relativa ou absoluta

`PocketBaseOptions.Url` decide:

- `"/"` → **mesma origem do app** (produção). O `PocketBaseClient` deriva a origem do `NavigationManager.BaseUri` — não precisa de JS interop.
- `"http://localhost:8090"` → URL absoluta (dev, ou PB em outro domínio).

`BaseUrl` já resolve os dois casos. **Nunca** monte a URL à mão nas chamadas.

#### ⚠️ `Uri.TryCreate(x, UriKind.Absolute)` mente no browser-wasm

O runtime WASM usa **semântica de caminho Unix**: `"/"` é um caminho absoluto. Então:

```csharp
// ❌ no browser-wasm: devolve TRUE e o Uri vira file:///
Uri.TryCreate("/", UriKind.Absolute, out var u);   // u.Scheme == "file"

// no desktop (Windows) devolve FALSE — o bug só aparece no navegador
```

Com `PocketBase:Url = "/"` (config de produção), o `BaseUrl` virava `"file:"`, a requisição saía como `file:/api/...` e o app quebrava no boot com:

```
System.UriFormatException: net_uri_BadAuthority   // "The hostname could not be parsed"
   at System.Uri..ctor(Uri baseUri, Uri relativeUri)
   at System.Net.Http.HttpClient.PrepareRequestMessage(HttpRequestMessage)
```

**Regra: só trate como URL absoluta o que tem esquema `http`/`https` explícito.** Use o helper `TryGetHttpUri` do `PocketBaseClient`:

```csharp
private static bool TryGetHttpUri(string? candidate, [NotNullWhen(true)] out Uri? uri)
{
    uri = null;
    if (string.IsNullOrWhiteSpace(candidate)) return false;
    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)) return false;
    if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
    if (string.IsNullOrEmpty(parsed.Host)) return false;
    uri = parsed;
    return true;
}
```

Qualquer coisa fora de http/https é **caminho relativo** → combina com `NavigationManager.BaseUri` (fallback: `HttpClient.BaseAddress`).

> Para reproduzir uma URL do WASM sem subir o app, use um harness de console que **linka o fonte real** do `PocketBaseClient.cs` e um `NavigationManager` fake — o `Uri` do desktop não reproduz a pegadinha.

---

## Estado compartilhado entre páginas

O `AuthService` é a fonte da verdade do usuário logado:

```csharp
@inject AuthService Auth

@if (Auth.IsAuthenticated)
{
    <span>@Auth.User?.Email</span>
}
```

Mudou o usuário? Chame `Auth.SetUser(user)` — ele dispara `AuthStateChanged` e o `AuthenticationStateProvider` atualiza as rotas protegidas.

Para estado que só interessa a uma página, use `@code { private bool _carregando; }` — sem serviço.

---

## Anti-padrões

| ❌ Não faça | ✅ Faça |
|---|---|
| `async void` em serviço | `async Task` |
| `HttpClient` novo por chamada | injete o `PocketBaseClient` (o `HttpClient` do host já existe) |
| `AddHttpClient()` | `AddScoped(_ => new HttpClient { BaseAddress = hostOrigin })` — o pacote `Microsoft.Extensions.Http` não está no projeto |
| `pocketbase.storage.get` no JS interop | `localStorage.getItem` — o global `pocketbase` é do SDK JS e não existe aqui |
| SDK NuGet do PocketBase | cliente fino do repo (sem risco de breaking change) |
| `try/catch` engolindo exceção | catch de `PocketBaseException` e trate a mensagem |
| `Uri.TryCreate(x, UriKind.Absolute)` para decidir se a config é URL | `TryGetHttpUri` (exige esquema `http`/`https`) — no WASM, `"/"` vira `file:///` |
| `StateHasChanged()` manual | o Blazor re-renderiza sozinho após `await` em handler |
| accessing `pb_data` direto do app | sempre pela API do PocketBase |

---

## Checklist

- [ ] Serviço registrado no `Program.cs` como `scoped`
- [ ] `HttpClient` registrado no `Program.cs` (se algum serviço o injeta)
- [ ] Nenhum segredo no código (token só via `AuthService`)
- [ ] Erros do PB tratados via `PocketBaseException`
- [ ] `dotnet publish` passa (`TreatWarningsAsErrors` está ligado)

## Skills relacionadas

- `.agents/skills/blazor-auth-guard/SKILL.md` — sessão e rotas protegidas
- `.agents/skills/blazor-page/SKILL.md` — página Razor consumindo o serviço
- `.agents/skills/pocketbase-collections/SKILL.md` — regras de acesso e collections
