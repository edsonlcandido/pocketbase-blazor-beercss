---
name: blazor-page
description: Criar uma página ou componente Razor (.razor) no app Blazor WebAssembly deste template — estrutura de pastas, ciclo de vida, formulários com EditForm e DataAnnotations, validação, estado de loading/erro/sucesso e componentes reutilizáveis. Use quando o pedido mencionar "cria uma página", "nova view", "formulário", "validação de form", "componente Razor", "adiciona uma tela" ou qualquer mudança em apps/web/Pages, apps/web/Components ou apps/web/Layout.
---

# Blazor — Páginas e componentes Razor

## Onde ficam

| Pasta | O que vai lá |
|---|---|
| `apps/web/Pages/` | páginas roteadas (`@page "/rota"`) |
| `apps/web/Components/` | componentes reutilizáveis (sem `@page`) |
| `apps/web/Layout/` | layout da SPA (`AppLayout.razor`) |
| `apps/landing/Pages/` | páginas da landing |

`_Imports.razor` já traz os `@using` de `Web`, `Web.Components`, `Web.Layout`, `Web.Services` — não repita.

---

## Esqueleto de uma página

```razor
@page "/orders"
@attribute [Authorize]          @* opcional — rota privada *@
@inject OrderService Orders     @* serviço via DI *@
@inject NavigationManager Navigation

<PageTitle>Pedidos · Web App</PageTitle>

<h1 class="h4 no-margin">Pedidos</h1>
<p class="muted">Lista de pedidos do usuário.</p>

@if (_loading)
{
    <div class="center-align top-margin large-padding">
        <progress class="circle" aria-label="Carregando"></progress>
    </div>
}
else if (!string.IsNullOrEmpty(_error))
{
    <FeedbackAlert Kind="FeedbackAlertKind.Error" Message="@_error" />
}
else
{
    <ul class="list border no-space">
        @foreach (var order in _orders)
        {
            <li>@order.Title</li>
        }
    </ul>
}

@code {
    private List<Order> _orders = new();
    private bool _loading = true;
    private string _error = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _orders = await Orders.ListAsync();
        }
        catch (PocketBaseException ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }
}
```

### Padrão de estado (loading / erro / sucesso)

O template usa **três campos** e o `FeedbackAlert` para o erro:

```csharp
private bool _loading;
private string _error = string.Empty;
private string _success = string.Empty;

private void ClearFeedback()
{
    _error = string.Empty;
    _success = string.Empty;
}
```

Sempre `try/catch/finally` — o `finally` sempre volta `_loading = false`.

---

## Formulários

```razor
<EditForm Model="_model" OnValidSubmit="SaveAsync">
    <DataAnnotationsValidator />

    <div class="field label border">
        <InputText @bind-Value="_model.Name" placeholder=" " autocomplete="name" />
        <label>Nome</label>
    </div>
    <ValidationMessage For="@(() => _model.Name)" />

    <div class="large-space"></div>

    <button type="submit" class="button" disabled="@_saving">
        @if (_saving)
        {
            <progress class="circle small"></progress>
            <span>Salvando...</span>
        }
        else
        {
            <i>check</i>
            <span>Salvar</span>
        }
    </button>
</EditForm>
```

### O modelo com DataAnnotations

```csharp
private sealed class OrderModel
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MinLength(3, ErrorMessage = "Mínimo de 3 caracteres.")]
    public string Name { get; set; } = string.Empty;
}
```

> ⚠️ **Condicional não vai no atributo.** `PasswordConfirm` com `[Compare]` falha no modo login (o campo nem é renderizado). Valide isso no código:
>
> ```csharp
> if (_isRegistering && _model.Password != _model.PasswordConfirm)
> {
>     _error = "As senhas não conferem.";
>     return;
> }
> ```

### Campo com label flutuante do BeerCSS

O `placeholder=" "` é **obrigatório** — o CSS usa `:placeholder-shown` pra decidir quando o label sobe. Sem ele, o label fica sempre por cima do texto.

---

## Upload de arquivo

```razor
<label class="button border">
    <i>image</i>
    <span>Escolher imagem</span>
    <InputFile OnChange="OnFileSelectedAsync" accept="image/*" />
</label>

@code {
    private const long MaxSize = 5 * 1024 * 1024;

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;

        await using var stream = file.OpenReadStream(MaxSize);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        var bytes = buffer.ToArray();
        // bytes -> ProfileService.UploadAvatarAsync(bytes, file.Name, file.ContentType)
    }
}
```

> ⚠️ `InputFile.OnChange` é `EventCallback<InputFileChangeEventArgs>` — **não** `IBrowserFile`. O arquivo vem de `args.File`.
>
> ⚠️ `OpenReadStream` só pode ser chamado **uma vez** por arquivo. Leia em memória e reaproveite os bytes.

---

## Componente reutilizável

```razor
@* apps/web/Components/StatCard.razor *@
<article class="card stat-card @Tone">
    <div class="row middle-align no-space">
        <div class="stat-icon"><i>@Icon</i></div>
        <div class="left-margin">
            <p class="small-text muted no-margin">@Label</p>
            <p class="h5 no-margin">@Value</p>
        </div>
    </div>
</article>

@code {
    [Parameter, EditorRequired] public string Icon { get; set; } = "insights";
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Value { get; set; } = string.Empty;
    [Parameter] public string Tone { get; set; } = "primary";
}
```

Convenções do template:

- `[Parameter, EditorRequired]` nos obrigatórios → erro de compilação se esquecer de passar.
- Parâmetros simples e primitivos. Estado interno só quando o componente **precisa** guardar algo.
- Classes CSS de layout ficam no `wwwroot/css/app.css` (não no `<style>` do componente) — assim o tema flat é único para os dois apps.

---

## Navegação

⚠️ **Use caminho RELATIVO (sem barra inicial).** O app roda na raiz em dev (`/`) e em `/app/` em produção, porque o `<base href>` muda. Caminho absoluto quebra em produção:

```csharp
// ❌ em produção (base "/app/") resolve para http://host/bio — fora do app
Navigation.NavigateTo("/bio");

// ✅ resolve contra o <base href>: "/bio" em dev, "/app/bio" em produção
Navigation.NavigateTo("bio");
```

O mesmo vale para links no markup:

```razor
@* ✅ relativo — o browser resolve contra o <base href> *@
<a href="bio">Minha bio</a>

@* ❌ absoluto — ignora o <base href> *@
<a href="/bio">Minha bio</a>
```

> Exceção: link para a **landing page** (que fica na raiz do domínio) é absoluto de propósito — `href="/"` — porque ela não faz parte do app.

### Rota `/` precisa existir

O template Vue fazia `redirect: '/dashboard'` na rota `/`. No Blazor não existe redirect automático — crie uma página que redireciona:

```razor
@* apps/web/Pages/Home.razor *@
@page "/"
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized()
        => Navigation.NavigateTo("dashboard", replace: true);
}
```

Sem essa página, abrir `/app/` (ou a raiz do dev server) mostra a **404** do `Router`.

### Estado entre rotas

Rotas são definidas pelo `@page`. Para passar estado, use query string:

```csharp
Navigation.NavigateTo($"orders/{id}");
```

```razor
@page "/orders/{id:int}"
@code {
    [Parameter] public int Id { get; set; }
}
```

---

## Anti-padrões

| ❌ Não faça | ✅ Faça |
|---|---|
| `NavigateTo("/bio")` (caminho absoluto) | `NavigateTo("bio")` — relativo ao `<base href>` |
| `<a href="/bio">` | `<a href="bio">` |
| `OnInitialized` com `async` sem `await` | `OnInitializedAsync` |
| `async void` | `async Task` |
| `catch (Exception)` genérico | catch de `PocketBaseException` |
| `StateHasChanged()` manual | o Blazor re-renderiza sozinho |
| `<style>` dentro do componente | classes no `wwwroot/css/app.css` |
| `@bind-Value` sem `Model` no `EditForm` | sempre `Model` + `OnValidSubmit` |
| string interpolada direto na URL com dados do usuário | `Uri.EscapeDataString` |

---

## Checklist

- [ ] `@page` único e coerente com a navegação
- [ ] Navegação com caminho **relativo** (sem `/` inicial)
- [ ] Loading, erro e sucesso tratados
- [ ] `try/catch/finally` com `_loading = false` no `finally`
- [ ] `ValidationMessage` para cada campo validado
- [ ] `dotnet publish` passa (`TreatWarningsAsErrors`)

## Skills relacionadas

- `.agents/skills/beercss-blazor/SKILL.md` — classes e componentes do BeerCSS
- `.agents/skills/blazor-service/SKILL.md` — consumindo dados do PocketBase
- `.agents/skills/blazor-auth-guard/SKILL.md` — protegendo a rota
