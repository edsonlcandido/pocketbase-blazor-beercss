---
name: pocketbase-gotchas
description: Referência rápida das pegadinhas do PocketBase 0.40.x neste template — routing de SPA com $apis.static e {path...}, flags de CLI, estrutura do Dockerfile multi-stage, URL do PocketBase no frontend Blazor em produção e comunicação entre serviços. Use quando o pedido mencionar routerAdd, $apis.static, pb_public, SPA routing 404, wildcard, Dockerfile do PocketBase, proxy reverso ou URL do PB no Blazor.
---

# PocketBase 0.40.x — routing, hooks e pegadinhas

> ⚠️ **Não invente sintaxe.** Referência oficial:
> - Routing: https://pocketbase.io/docs/js-routing/
> - JS overview: https://pocketbase.io/docs/js-overview/

## Versão

- PocketBase **0.40.5** (linux_amd64 / darwin / windows)
- Verificar: `./pocketbase --version`
- Versões antigas (0.22.x) têm APIs diferentes — não copiar código de lá.

---

## Rota catch-all pra SPA

A sintaxe correta é **`{path...}`** (três pontos) — é o catch-all do Go. `{path?}` **não existe**.

```js
// ❌ não existe
routerAdd("GET", "/app/{path?}", ...)
// ❌ só captura 1 segmento
routerAdd("GET", "/app/{path}", ...)

// ✅ CERTO
routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
```

| Sintaxe | Significado |
|---|---|
| `{path}` | parâmetro único |
| `{path...}` | catch-all (múltiplos segmentos) — **3 pontos** |
| `{path?}` | **não existe** |

### Servir arquivos estáticos

```js
// serve o diretório inteiro, com fallback de SPA
routerAdd("GET", "/{path...}", $apis.static("pb_public", true))
routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
```

**Parâmetros de `$apis.static(dirPath, spaFallback)`:**

- `dirPath` — caminho do diretório, relativo ao working dir do PB (`/app` na imagem Docker)
- `spaFallback` — `false` = 404 se não achar; `true` = devolve o `index.html` (modo SPA)

No template, a **landing** é servida direto pelo PB (sem hook: `pb_public/` é a raiz estática do PB) e o **web app** precisa do hook `main.pb.js` com `spaFallback = true`.

### Ordem de registro

O PB processa os arquivos de `pb_hooks/` em **ordem alfabética**. Se existir uma rota de API e um catch-all, o catch-all ganha se for registrado primeiro. Use prefixo numérico no nome do arquivo:

```
pb_hooks/
├── 00-api.pb.js        # rotas de API (registradas primeiro)
└── main.pb.js          # catch-all /app/{path...}
```

---

## Hook completo do template (SPA Blazor)

```js
/// <reference path="../pb_data/types.d.ts" />

// Landing na raiz é servida pelo PB automaticamente (pb_public/index.html)
// Web app em /app/*
routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
```

> O `true` (SPA fallback) é o que faz `/app/dashboard` devolver o `index.html` do Blazor em vez de 404 — o roteamento no cliente é quem decide a tela depois.

---

## Dockerfile — versão via ARG

```dockerfile
ARG POCKETBASE_VERSION=0.40.5

RUN wget https://github.com/pocketbase/pocketbase/releases/download/v${POCKETBASE_VERSION}/pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && unzip pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && rm pocketbase_${POCKETBASE_VERSION}_linux_amd64.zip \
    && chmod +x pocketbase

CMD ["./pocketbase", "serve", "--http=0.0.0.0:8090"]
```

> ⚠️ **Não use imagem do Docker Hub** — `pocketbase/pocketbase` não existe (oficial é `ghcr.io/pocketbase/pocketbase`).

### Módulos .NET no Dockerfile

Cada app Blazor é um projeto .NET independente. O Dockerfile faz `dotnet publish` em estágios separados e **não** usa Node:

```dockerfile
# ✅ CERTO — copia o app inteiro (csproj + .razor + wwwroot)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS web-builder
WORKDIR /src
COPY apps/web ./apps/web
RUN dotnet publish apps/web -c Release -o /out/web
```

```dockerfile
# ❌ ERRADO — copiar só o csproj quebra o build
COPY apps/web/Web.csproj ./apps/web/
```

Motivo: o `dotnet publish` precisa do **código-fonte inteiro** (todos os `.razor`, `Program.cs`, `Services/`, `Layout/`) e do `wwwroot` (HTML, CSS vendorizado, ícones). Copiar só o `.csproj` dá erro de compilação.

**Pegadinha do Blazor WASM:** o output do publish é `publish/wwwroot/_framework/` — o runtime inteiro do .NET (wasm + JS). **Não copie só o `index.html`**: o PB tem que servir `_framework/` junto, senão o app não boota ("Failed to fetch resource: _framework/dotnet.js").

```dockerfile
# ✅ o wwwroot inteiro (com _framework/)
COPY --from=web-builder /out/web/wwwroot ./pb_public/app
```

> **Não usei `--no-restore`**: sem lock file restored o build falha offline. Também **não** copie `bin/`/`obj/` do host — deixe o Docker restaurar do zero (o `.dockerignore` garante isso).

---

## URL do PocketBase no frontend (Blazor)

Em produção o app Blazor é servido pelo **mesmo PocketBase** → mesma origem. A URL vem de um **arquivo de runtime** (`wwwroot/appsettings.json`), não de variável de ambiente nem de constante no código:

```jsonc
// apps/web/wwwroot/appsettings.json — valor de DEV
{
  "PocketBase": {
    "Url": "http://localhost:8090"
  }
}
```

O `PocketBaseOptions` carrega esse JSON via `HttpClient.GetFromJsonAsync("appsettings.json")` no `Program.cs`. O `PocketBaseClient` resolve:

- URL **absoluta** (`http://localhost:8090`) → usa como está (dev).
- URL **relativa** (`/`) → prefixa a origem do app via `NavigationManager.BaseUri` (produção, mesma origem).

```csharp
// apps/web/Services/PocketBaseClient.cs
if (Uri.TryCreate(configured, UriKind.Absolute, out var absolute))
    return (absolute.GetLeftPart(UriPartial.Authority) + absolute.AbsolutePath).TrimEnd('/');

var origin = new Uri(_navigation.BaseUri).GetLeftPart(UriPartial.Authority);
return (origin + configured).TrimEnd('/');
```

> ⚠️ **Não confie em `new URL(request.url).origin` no servidor.** Atrás de proxy (Easypanel, Nginx, Cloudflare), o `Host` que chega é o interno (`localhost:3000`) e `X-Forwarded-Host` pode nem ser enviado — o `origin` sai errado. Resolver pela origem **do lado do cliente** (como o `PocketBaseClient` faz) é o que funciona.

### Injetar a URL no build de produção

O `appsettings.json` de produção é **sobrescrito no publish** por um target MSBuild:

```xml
<!-- apps/web/Web.csproj -->
<PropertyGroup>
  <PocketBaseUrl Condition="'$(PocketBaseUrl)' == ''">/</PocketBaseUrl>
</PropertyGroup>

<Target Name="WritePocketBaseUrl" AfterTargets="Publish">
  <WriteLinesToFile
    File="$([System.IO.Path]::Combine('$(PublishDir)', 'wwwroot', 'appsettings.json'))"
    Overwrite="true"
    WriteOnlyWhenDifferent="true"
    Lines="{ &quot;PocketBase&quot;: { &quot;Url&quot;: &quot;$(PocketBaseUrl)&quot; } }" />
</Target>
```

```bash
# mesma origem (padrão)
/ dotnet publish apps/web -c Release -o apps/web/publish

# PB em outro host
dotnet publish apps/web -c Release -o apps/web/publish -p:PocketBaseUrl=https://api.exemplo.com
```

> `WriteLinesToFile` sem `File=` dá **MSB4044**. E o `File` tem que apontar pra `$(PublishDir)/wwwroot/...` — `$(PublishDir)` **já é** o `wwwroot` no Blazor WASM? **Não**: no WASM o publish gera `publish/wwwroot/`, então o caminho correto é `publish/wwwroot/appsettings.json`.
>
> O mesmo padrão vale pra `WebAppUrl` em `apps/landing/Landing.csproj`.

### Base href do Blazor (dev vs prod)

O Blazor WASM descobre as rotas do **`<base href>`** do `index.html`. Em dev o app fica na raiz; em prod ele é servido em `/app/`. O `index.html` do web app resolve isso em runtime:

```html
<script>
  (function () {
    var m = /^(.*)\/app(\/.*)?$/.exec(location.pathname);
    document.write('<base href="' + (m ? '/app/' : '/') + '">');
  })();
</script>
```

> Sem isso, o roteador do Blazor acha que a base é `/` e as rotas `/app/dashboard` não resolvem (nem na URL nem nos assets `_framework/`).

> A URL do PB é **runtime** (o `appsettings.json` é lido pelo app já carregado) — dá pra trocar sem rebuildar. Já o **`<base href>`** é resolvido no boot, então mudar o prefixo de deploy exige rebuild.

---

## Comunicação entre serviços

Quando o template ganhar um backend extra (API Python/Node), use o **hostname Docker interno**, nunca a URL pública:

```env
# serviço que fala com o PB
POCKETBASE_URL=http://pocketbase:8090
# serviço que é chamado pelo PB
API_URL=http://api:8000
```

No hook, leia do ambiente com default:

```js
const base = $os.getenv("API_URL") || "http://api:8000";
```

Rede Docker interna: nenhum serviço precisa expor porta pública se só fala com os outros.

---

## Testar localmente

```bash
# 1) Build dos dois apps (o MSBuild já copia pro pb_public)
dotnet publish apps/landing -c Release -o apps/landing/publish
dotnet publish apps/web     -c Release -o apps/web/publish

# 2) Subir o PocketBase
docker compose up -d

# 3) Ou manualmente (com o binário baixado)
# cd pocketbase && ./pocketbase serve --http=127.0.0.1:8090
```

Acesso: landing em `/`, web app em `/app/`, admin em `/_/`.

> **A cópia pro `pb_public` é feita pelo MSBuild** (alvo `CopyToPocketBase`, em `Directory.Build.targets`): `apps/landing` → `pb_public/`, `apps/web` → `pb_public/app/`. A limpeza usa um manifesto (`.published.txt`) — só o que o app publicou é apagado, então os dois builds não se derrubam.

---

## Erros comuns

| Erro | Causa | Solução |
|---|---|---|
| `bad wildcard name "path?"` | `{path?}` não existe | `{path}` (1 segmento) ou `{path...}` (catch-all) |
| `Object has no member 'static'` | versão muito antiga do PB | subir pra 0.40.x |
| `MSB4044` no build Docker | `WriteLinesToFile` sem `File=` | apontar pra `$(PublishDir)/wwwroot/appsettings.json` |
| `CS0234` / `CS0246` no build Docker | copiou só o `.csproj` | copiar o app inteiro (`COPY apps/web ./apps/web`) |
| SPA dá 404 em `/app/rota` | falta o `spaFallback = true` | `$apis.static("pb_public/app", true)` |
| Rota de API devolve o HTML da SPA | catch-all registrado antes | prefixo numérico no arquivo do hook |
| 404 em `/api/...` no browser mas funciona no curl | URL do PB errada no front | `appsettings.json` com `Url: "/"` (mesma origem) |
| **404 silencioso** em `/api/collections/users/records/{id}` | header `Authorization: Token <token>` | token **cru**: `Authorization: <token>` |
| 404 em `/api/collections/users/{id}` | faltou `/records` no path | `GET/PATCH /api/collections/users/records/{id}` |
| App não boota: "Failed to fetch ... _framework/dotnet.js" | `pb_public/app/` sem o `_framework/` | copiar o `wwwroot` inteiro do publish |
| App não boota: `CannotResolveService, System.Net.Http.HttpClient` | `HttpClient` não registrado no DI (não vem por default no WASM) | `AddScoped(_ => new HttpClient { BaseAddress = hostOrigin })` no `Program.cs` |
| App não boota: `Could not find 'pocketbase.storage.get'` | global do SDK JS do PocketBase (não existe aqui) | usar `localStorage.getItem` via `IJSRuntime` |
| Rota do Blazor não resolve em `/app/...` | `<base href>` fixo em `/` | `<base href>` dinâmico no `index.html` |
| 404 em `/app/` | não existe rota `/` | criar `Pages/Home.razor` com `@page "/"` que redireciona |
| Link `/bio` dá 404 em produção | caminho absoluto ignora o `<base href>` | usar caminho relativo (`bio`) |
| `blazor.boot.json` 404 | no .NET 10 ele não existe mais | o manifest é embutido no `blazor.webassembly.js` — não é bug |

---

## Migrations JS (PB 0.40+)

> **Skill dedicada:** `.agents/skills/pocketbase-migrations/SKILL.md` — cobre em profundidade as 3 regras, sintaxe de field, timestamps, idempotência, teste local com `pb_data` + `sqlite3` e tabela de erros.

Resumo:

- **NUNCA editar uma migration já commitada** → o PB não reaplica. Correção = migration nova, idempotente, com timestamp maior.
- **Opções de field diretas no field** (PB 0.40+): `values`, `collectionId`, `maxSelect`, `cascadeDelete` — **não** dentro de `options: {}`.
- **Field novo em collection existente**: `collection.fields.add(new Field({...}))` (objeto literal falha).
- **Testar antes de commitar**: subir o PB com `pb_data` limpo e conferir o startup + `sqlite3`.
