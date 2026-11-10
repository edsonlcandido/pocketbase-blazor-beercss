---
name: blazor-config
description: Configurar as URLs e o build de produção dos apps Blazor WebAssembly deste template — appsettings.json de dev vs prod, o target MSBuild que injeta a URL no publish, o base href e o caminho de montagem /app, CORS com o PocketBase, override via -p: e publicação do build estático no pb_public. Use quando o pedido mencionar "aponta pra outro PocketBase", "url da api", "config", "env", "produção", "publish", "base href", "deploy", "mesma origem" ou qualquer mudança em apps/*/wwwroot/appsettings.json, *.csproj ou index.html.
---

# Blazor — Configuração, URLs e build

## Onde a configuração vive

| Arquivo | Quando |
|---|---|
| `apps/web/wwwroot/appsettings.json` | **DEV** — valor padrão, versionado no git |
| `apps/landing/wwwroot/appsettings.json` | **DEV** — valor padrão, versionado no git |
| `apps/<app>/wwwroot/appsettings.json` (no `publish/`) | **PROD** — sobrescrito pelo target MSBuild |

```json
// apps/web/wwwroot/appsettings.json (DEV)
{ "PocketBase": { "Url": "http://localhost:8090" } }

// apps/landing/wwwroot/appsettings.json (DEV)
{ "WebApp": { "Url": "http://localhost:5174" } }
```

> ⚠️ O arquivo é **servido publicamente**. Nunca coloque segredo aqui — use `$os.getenv` em hook do PocketBase.

---

## O target que injeta a URL de produção

Em `apps/web/Web.csproj`:

```xml
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

O `Landing.csproj` tem o equivalente com `WebAppUrl` (default `/app/`).

**Override no publish:**

```bash
dotnet publish apps/web -c Release -o apps/web/publish -p:PocketBaseUrl=https://api.exemplo.com
dotnet publish apps/landing -c Release -o apps/landing/publish -p:WebAppUrl=https://app.exemplo.com
```

**Não edite o valor de prod no fonte** — edite a propriedade MSBuild (ou passe `-p:`). O valor no `appsettings.json` do fonte é o de **dev**.

---

## URL relativa vs absoluta

| Valor | Significado |
|---|---|
| `/` | **mesma origem do app** — produção (PB serve app + API juntos) |
| `http://localhost:8090` | URL absoluta — dev, ou PB em outro domínio |

O `PocketBaseClient` resolve os dois:

```csharp
// URL absoluta → usa como está
if (Uri.TryCreate(configured, UriKind.Absolute, out var absolute)) { ... }

// URL relativa → mesma origem do app, via NavigationManager.BaseUri
var origin = new Uri(_navigation.BaseUri).GetLeftPart(UriPartial.Authority);
return (origin + configured).TrimEnd('/');
```

**Não monte a URL à mão** nas chamadas — use `BaseUrl` do cliente.

---

## O `<base href>` e o caminho de montagem

Em dev o app vive na raiz (`http://localhost:5174/`). Em prod o PocketBase serve em `/app/`.

O `apps/web/wwwroot/index.html` resolve isso **antes** do Blazor subir:

```html
<base href="/" />
<script>
    (function () {
        var match = window.location.pathname.match(/^(.*)\/app(\/.*)?$/);
        if (match) {
            document.querySelector('base').setAttribute('href', match[1] + '/app/');
        }
    })();
</script>
```

> ⚠️ **Se você mudar o caminho de montagem, atualize a regex** e o hook `pocketbase/pb_hooks/main.pb.js`:
>
> ```js
> routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
> ```

A landing é servida na raiz — o `<base href="/">` dela funciona em dev e prod, sem script.

### Consequência: navegação relativa

Como o `<base href>` muda entre dev (`/`) e prod (`/app/`), **todo caminho interno do app tem que ser relativo**:

```csharp
Navigation.NavigateTo("bio");   // ✅ "/bio" em dev, "/app/bio" em prod
Navigation.NavigateTo("/bio");  // ❌ "/bio" em dev, "/bio" em prod (fora do app)
```

O mesmo vale para `<a href="bio">` no markup. Detalhes em `.agents/skills/blazor-page/SKILL.md`.

> A URL do PocketBase (`PocketBase:Url`) é **runtime** — o `appsettings.json` é lido pelo app já carregado, então dá pra trocar sem rebuildar. Já o `<base href>` é resolvido no boot: mudar o prefixo de deploy exige rebuild.

---

## CORS

O PocketBase **permite todas as origens por padrão** (`--origins` default `[*]`). É isso que deixa o app em `:5174` falar com a API em `:8090` em dev.

Se você restringir o CORS no Admin do PocketBase, inclua:

```
http://localhost:5173
http://localhost:5174
```

Em produção o app é same-origin → CORS não entra.

---

## O build de produção

```bash
dotnet publish apps/web -c Release -o apps/web/publish          # já copia pro pocketbase/pb_public/app
dotnet publish apps/landing -c Release -o apps/landing/publish  # já copia pro pocketbase/pb_public
```

O `dotnet publish` do Blazor WASM gera:

```
apps/web/publish/
├── wwwroot/            ← ISTO vai pro pb_public/app
│   ├── index.html
│   ├── _framework/     ← runtime do .NET (blazor.webassembly.js, *.wasm)
│   ├── css/ js/ beercss/
│   └── appsettings.json  (com a URL de PROD)
├── Web.dll
└── ...
```

### A cópia pro `pb_public` (alvo MSBuild)

Quem faz a cópia é o alvo `CopyToPocketBase`, em `Directory.Build.targets` (raiz do repo). Ele roda depois do `Publish` e copia `$(PublishDir)wwwroot` para a pasta definida em `$(PocketBasePublicDir)`:

| App | `PocketBasePublicDir` | Servido em |
|---|---|---|
| `apps/landing` | `pocketbase/pb_public` | `/` |
| `apps/web` | `pocketbase/pb_public/app` | `/app/` |

A limpeza é feita por **manifesto** (`.published.txt`, gravado na pasta de destino): só os arquivos que o app publicou na última build são apagados. Sem isso, o build da landing apagaria o build do web app (e vice-versa) — os dois moram dentro de `pb_public`.

Para desligar a cópia (build só de validação):

```bash
dotnet publish apps/web -c Release -p:SkipPocketBaseCopy=true
```

> ⚠️ **Comment XML não pode conter `--`.** Um `---` dentro do comment do `Directory.Build.targets` dá `MSB4024: An XML comment cannot contain '--'`.

> ⚠️ No .NET 10 **não existe mais `blazor.boot.json`** — o boot manifest é embutido no `blazor.webassembly.js`. Se você procurar esse arquivo e não achar, não é bug.

---

## Onde o PocketBase serve os arquivos

O PB 0.40 usa (relativo ao diretório do binário):

| Caminho | Default |
|---|---|
| `pb_data/` | `--dir` |
| `pb_public/` | `--publicDir` ← **irmão** de `pb_data`, não dentro |
| `pb_hooks/` | `--hooksDir` |
| `pb_migrations/` | `--migrationsDir` |

Conferir de primeira mão:

```bash
./pocketbase serve --help
```

---

## Checklist

- [ ] `appsettings.json` do fonte com o valor de **dev**
- [ ] Target MSBuild presente no `.csproj` do app
- [ ] `PocketBaseUrl` / `WebAppUrl` com o valor de **prod** no publish
- [ ] `<base href>` com o script de ajuste se o app não for servido na raiz
- [ ] Hook `routerAdd("GET", "/app/{path...}", ...)` coerente com o caminho de montagem
- [ ] Nenhum segredo em `appsettings.json`

## Skills relacionadas

- `.agents/skills/blazor-service/SKILL.md` — cliente do PocketBase
- `.agents/skills/pocketbase-gotchas/SKILL.md` — routing, SPA e Docker do PB
- `AGENTS.md` (raiz) — configuração completa do template
