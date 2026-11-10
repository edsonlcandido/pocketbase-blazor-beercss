---
name: blazor-auth-guard
description: Proteger rotas, redirecionar usuário deslogado, gerenciar a sessão (token no localStorage, revalidação, logout) e criar páginas que exigem autenticação no app Blazor WebAssembly deste template. Cobre o ciclo do AuthService, o AuthenticationStateProvider próprio, [Authorize], AuthorizeRouteView e oRedirectToLogin. Use quando o pedido mencionar "protege a rota", "rota privada", "redireciona pro login", "sessão", "logout", "token", "não deixa acessar sem login" ou qualquer mudança em apps/web/App.razor, AuthService, ProfileService ou nas Pages.
---

# Blazor — Auth, sessão e rotas protegidas

O template **não** usa o sistema de auth do ASP.NET Core. Ele tem um `AuthenticationStateProvider` próprio, alimentado pelo `AuthService`, que fala com o PocketBase.

## O fluxo, de ponta a ponta

```
App.razor (OnInitializedAsync)
   └─ await Auth.InitializeAsync()      ← lê o token do localStorage e revalida
        └─ AuthService
             ├─ PocketBaseClient (Authorization: <token> cru)
             └─ BrowserStorage (localStorage, chave "pocketbase_auth")

App.razor (AuthorizeRouteView)
   └─ NotAuthorized → <RedirectToLogin /> → NavigateTo("login")
```

---

## 1. Proteger uma página

```razor
@page "/dashboard"
@attribute [Authorize]

<h1>Dashboard</h1>
```

Pronto. O `AuthorizeRouteView` no `App.razor` cuida do resto.

**Requisito:** `builder.Services.AddAuthorizationCore()` no `Program.cs` (já está) e o `PocketBaseAuthenticationStateProvider` registrado (já está).

---

## 2. Rota liberada para quem já está logado

```razor
@page "/login"

@code {
    protected override void OnInitialized()
    {
        if (Auth.IsAuthenticated)
        {
            Navigation.NavigateTo("dashboard");
        }
    }
}
```

> O caminho é **relativo** (sem `/` inicial) — resolve contra o `<base href>`, que é `/` em dev e `/app/` em produção. Ver `.agents/skills/blazor-page/SKILL.md`.

É o `Pages/Login.razor` do template.

---

## 3. Redirecionar por falta de permissão (ex.: só admin)

O `NotAuthorized` do `App.razor` tem dois caminhos: sem token → `/login`; com token sem permissão → "Acesso negado".

```razor
<NotAuthorized>
    @if (context.User.Identity?.IsAuthenticated != true)
    {
        <RedirectToLogin />
    }
    else
    {
        <p>Acesso negado.</p>
    }
</NotAuthorized>
```

Para uma página que exige **role**, use `[Authorize(Roles = "admin")]` e garanta que a claim exista no `PocketBaseAuthenticationStateProvider`.

---

## 4. O `RedirectToLogin`

```razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized()
    {
        Navigation.NavigateTo("login", replace: true);
    }
}
```

`replace: true` evita poluir o histórico do browser com o redirect.

---

## 5. O ciclo da sessão

### `AuthService` (a fonte da verdade)

```csharp
public PbUser? User { get; private set; }
public bool IsAuthenticated => User is not null;
public event Action? AuthStateChanged;
```

| Método | O que faz |
|---|---|
| `InitializeAsync()` | Lê o token do `localStorage`, chama `auth-refresh`, valida a sessão |
| `LoginAsync(email, senha)` | Autentica e **persiste** o token |
| `RegisterAsync(...)` | Cria o registro e faz login em seguida |
| `LogoutAsync()` | Limpa token, usuário e `localStorage` |
| `SetUser(user)` | Atualiza o usuário em memória (após salvar o perfil) |

### `InitializeAsync` roda ANTES da primeira renderização

Isso é o que impede uma rota protegida de redirecionar para o login antes do token ser lido:

```razor
@code {
    protected override async Task OnInitializedAsync()
    {
        await Auth.InitializeAsync();
    }
}
```

> ⚠️ **Não remova esse `await`.** Sem ele, o `AuthorizeRouteView` avalia "não autenticado" antes da sessão ser restaurada e joga o usuário no login a cada F5.

### Token inválido

`InitializeAsync` trata **qualquer** exceção chamando `LogoutAsync()` — a sessão morta é limpa sozinha e o app segue para o login.

> ⚠️ **O catch é largo de propósito.** Como o `await Auth.InitializeAsync()` roda **antes** da primeira renderização, qualquer exceção que escape daqui derruba o app inteiro (tela de erro, nada renderizado). Isso inclui `UriFormatException` de URL mal montada e `HttpRequestException` de PocketBase fora do ar. A sessão salva no navegador nunca pode impedir o app de subir.

---

## 6. Logout

```razor
<button type="button" class="error" @onclick="LogoutAsync">
    <i>logout</i>
    <span>Sair</span>
</button>

@code {
    private async Task LogoutAsync()
    {
        await Auth.LogoutAsync();
        Navigation.NavigateTo("login");
    }
}
```

`LogoutAsync` limpa o `localStorage`. Se quiser invalidar o token **no servidor**, chame `POST /api/collections/users/auth-refresh`… não — para logout real o PB usa `DELETE /api/collections/users/auth-refresh` (PB 0.23+). O template optou por só limpar o token local (o token expira sozinho).

---

## 7. Onde o token fica

| Item | Valor |
|---|---|
| Storage | `window.localStorage` |
| Chave | `pocketbase_auth` |
| Conteúdo | o token **cru** (string) |
| Header na API | `Authorization: <token>` (sem prefixo) |

Trocar a chave é o jeito mais fácil de invalidar todas as sessões.

### Como o `localStorage` é acessado

O `BrowserStorage` usa JS interop com a API **nativa** do browser:

```csharp
_js.InvokeAsync<string?>("localStorage.getItem", key);
_js.InvokeVoidAsync("localStorage.setItem", key, value);
_js.InvokeVoidAsync("localStorage.removeItem", key);
```

> ⚠️ **Não use `pocketbase.storage.get`/`set`/`remove`.** Esse global pertence ao **SDK JS** do PocketBase — ele existia no template Vue, mas aqui o cliente é 100% C# e o SDK não está na página. O erro aparece assim:
>
> ```
> Could not find 'pocketbase.storage.get' ('pocketbase' was undefined).
> ```
>
> E é fatal: acontece dentro do `Auth.InitializeAsync()`, que roda **antes** da primeira renderização — o app fica com a tela de erro "Ocorreu um erro inesperado" e não renderiza nada.

---

## Anti-padrões

| ❌ Não faça | ✅ Faça |
|---|---|
| Ler o `localStorage` direto na página | `AuthService` / `BrowserStorage` |
| `pocketbase.storage.get` no JS interop | `localStorage.getItem` — o global `pocketbase` é do SDK JS |
| `Navigation.NavigateTo("/login")` | `NavigateTo("login")` — relativo ao `<base href>` |
| Guardar senha ou token em campo de página | só o token, via `AuthService` |
| `@attribute [Authorize]` sem `AddAuthorizationCore()` | registre no `Program.cs` |
| Remover o `await Auth.InitializeAsync()` do `App.razor` | ele é o que restaura a sessão |
| `Navigation.NavigateTo` no `OnAfterRender` de rota protegida | use `AuthorizeRouteView` + `RedirectToLogin` |
| Confiar só no `localStorage` para autorizar | o `auth-refresh` valida no servidor |

---

## Checklist

- [ ] Página privada tem `@attribute [Authorize]`
- [ ] `Program.cs` tem `AddAuthorizationCore()` + o `AuthenticationStateProvider`
- [ ] `App.razor` faz `await Auth.InitializeAsync()` antes de renderizar
- [ ] Logout limpa o `localStorage`
- [ ] `dotnet publish` passa

## Skills relacionadas

- `.agents/skills/blazor-service/SKILL.md` — `PocketBaseClient`, `AuthService`, DI
- `.agents/skills/blazor-page/SKILL.md` — página Razor com form e feedback
