# PocketBase + Blazor WebAssembly + BeerCSS — Template

Template de backend + frontend: **PocketBase** como API/auth/DB e **monorepo Blazor WebAssembly (.NET 10) + BeerCSS** como interface. Usado como base para projetos novos.

> Este arquivo é o contexto operacional para agentes de IA (OpenCode, Copilot, Claude Code, Codex…).
> Ele descreve **o que fazer neste repositório**, não o produto de cada projeto que consome o template.
> Skills com comandos prontos ficam em `.agents/skills/` — índice em `.agents/skills/README.md`.

---

## 1. Sobre o projeto

| Camada | Stack |
|---|---|
| Backend / DB / Auth | PocketBase **0.40.5** (SQLite + Go), JSVM (hooks + migrations) |
| Frontend | **Blazor WebAssembly** (.NET 10) + **BeerCSS 5.0.3** (Material Design 3) |
| Estado / rotas | `AuthenticationStateProvider` próprio + `AuthorizeRouteView` |
| Monorepo | `apps/landing` + `apps/web` (dois projetos .NET independentes) |
| Produção | Dockerfile multi-stage → 1 imagem com PB + landing + web app |

Requisitos: **.NET SDK 10** (8 e 6 também funcionam, mas o alvo é o 10) e **Docker** (para o PocketBase local e a imagem de produção).

> **Sem Node, sem npm.** Não existe `package.json` nem `node_modules` neste repositório — a orquestração de build é feita por alvos MSBuild (`Directory.Build.targets`).

### O que é cada app

| App | Path | Rota em prod | Papel |
|---|---|---|---|
| `landing` | `apps/landing` | `/` | Página pública de apresentação/marketing |
| `web` | `apps/web` | `/app/` | SPA logada (login, dashboard, perfil) |

Em desenvolvimento as duas rodam em servidores do .NET (5173 / 5174) e o PocketBase em 8090. Em produção **as duas são servidas pelo PocketBase na mesma origem** (por isso `PocketBase:Url = /`).

---

## 2. Estrutura

```
pocketbase-blazor-beercss/
├── apps/
│   ├── landing/                  # landing (Blazor WASM + BeerCSS)
│   │   ├── Program.cs            # DI + host
│   │   ├── App.razor             # <Router>
│   │   ├── LandingOptions.cs     # URL do web app
│   │   ├── Pages/Home.razor
│   │   ├── Properties/launchSettings.json   # porta 5173
│   │   └── wwwroot/
│   │       ├── index.html        # <base href> + script de base path
│   │       ├── appsettings.json  # config de DEV
│   │       ├── css/app.css       # tema flat + estilos do app
│   │       └── beercss/          # BeerCSS vendorizado (css + fonte)
│   │
│   └── web/                      # SPA logada (Blazor WASM + BeerCSS)
│       ├── Program.cs
│       ├── App.razor             # <Router> + <AuthorizeRouteView>
│       ├── Layout/AppLayout.razor
│       ├── Pages/{Home,Login,Dashboard,Bio}.razor   # Home = redirect p/ dashboard
│       ├── Components/{StatCard,FeedbackAlert,RedirectToLogin}.razor
│       ├── Services/
│       │   ├── PocketBaseClient.cs        # cliente HTTP do PB (sem SDK externo)
│       │   ├── PocketBaseModels.cs
│       │   ├── PocketBaseOptions.cs
│       │   ├── AuthService.cs             # login/cadastro/logout/sessão
│       │   ├── ProfileService.cs
│       │   ├── BrowserStorage.cs           # localStorage via JS interop
│       │   └── PocketBaseAuthenticationStateProvider.cs
│       ├── Properties/launchSettings.json   # porta 5174
│       └── wwwroot/
│           ├── index.html
│           ├── appsettings.json  # config de DEV
│           ├── css/app.css
│           ├── js/app.js         # helpers de JS interop
│           └── beercss/
│
├── pocketbase/
│   ├── pb_hooks/main.pb.js       # SPA routing: /app/{path...}
│   ├── pb_migrations/            # migrations JS (versionadas no git)
│   ├── pb_data/                  # estado do PB (gitignored, exceto types.d.ts)
│   └── pb_public/                # estáticos servidos pelo PB
│       ├── (landing build)       # → /
│       └── app/                  # (web build) → /app/
│
├── Directory.Build.targets   # alvo CopyToPocketBase (copia o build pro pb_public)
│
├── Dockerfile                # multi-stage: landing-builder → web-builder → alpine+PB
├── docker-compose.yaml       # PB local (alvo "pocketbase" do Dockerfile)
├── AGENTS.md                 # este arquivo
└── .agents/skills/           # skills pra agentes de IA
```

---

## 3. Comandos

```bash
# desenvolvimento (3 terminais)
docker compose up -d                          # PocketBase :8090 (API + admin + build estático)
dotnet watch run --project apps/landing        # landing :5173 (hot reload)
dotnet watch run --project apps/web            # web app :5174 (hot reload)

# build (o MSBuild já copia o resultado pro pocketbase/pb_public)
dotnet publish apps/landing -c Release -o apps/landing/publish
dotnet publish apps/web     -c Release -o apps/web/publish

# build só para validar a compilação (sem copiar pro pb_public)
dotnet publish apps/web -c Release -p:SkipPocketBaseCopy=true

# preview do resultado final (PB servindo os builds)
docker compose up -d                          # http://localhost:8090/ e /app/

# imagem de produção
docker build -t pocketbase-blazor-beercss .
```

`TreatWarningsAsErrors` está **ligado** nos dois `.csproj` — warning de compilação **quebra o build**.

### Como o build chega no PocketBase

O alvo `CopyToPocketBase` (em `Directory.Build.targets`, na raiz) roda depois do `dotnet publish`:

| App | `PocketBasePublicDir` | Servido em |
|---|---|---|
| `apps/landing` | `pocketbase/pb_public` | `/` |
| `apps/web` | `pocketbase/pb_public/app` | `/app/` |

A limpeza usa um manifesto (`.published.txt`) com a lista de arquivos da última publicação: **só o que o app publicou é apagado** — o build da landing nunca derruba o build do web app (e vice-versa), mesmo morando os dois dentro de `pb_public`.

> Para desligar a cópia: `-p:SkipPocketBaseCopy=true`.

### Sem Docker?

Baixe o binário do PocketBase 0.40.5 e rode **de dentro da pasta `pocketbase/`** (é de lá que ele lê `pb_hooks/`, `pb_migrations/`, `pb_data/` e `pb_public/`):

```bash
./pocketbase serve --http=127.0.0.1:8090
```

### Serviços e URLs (dev)

| Serviço | URL |
|---|---|
| PocketBase (API + admin) | http://localhost:8090 |
| Admin do PocketBase | http://localhost:8090/_/ |
| Landing | http://localhost:5173 |
| Web app | http://localhost:5174 |

### Endereços em produção

| Caminho | Servido por |
|---|---|
| `/` | landing build (`pb_public/`) |
| `/app/**` | web app build (`pb_public/app/`) — via hook `main.pb.js` |
| `/api/**` | REST do PocketBase |
| `/_/**` | Admin do PocketBase |

---

## 4. Configuração

O Blazor WASM lê a configuração de `wwwroot/appsettings.json` **em runtime** (não é build-time como o `VITE_*` do template Vue).

| Arquivo | Git | Quando |
|---|---|---|
| `apps/<app>/wwwroot/appsettings.json` | ✅ | valor de DEV, usado por `dotnet run`/`dotnet watch` |
| `apps/<app>/wwwroot/appsettings.json` (no publish) | — | sobrescrito pelo target MSBuild com o valor de PROD |

| Variável | App | Dev | Prod |
|---|---|---|---|
| `PocketBase:Url` | web | `http://localhost:8090` | `/` (mesma origem) |
| `WebApp:Url` | landing | `http://localhost:5174` | `/app` |

**Override no publish:**

```bash
dotnet publish apps/web -c Release -o apps/web/publish -p:PocketBaseUrl=https://api.exemplo.com
dotnet publish apps/landing -c Release -o apps/landing/publish -p:WebAppUrl=https://app.exemplo.com
```

> **Nunca** coloque segredo em `appsettings.json` — ele é servido publicamente. Use hooks do PocketBase (`$os.getenv`) pra dado sensível.

### CORS

O PocketBase **permite todas as origens por padrão** (`Access-Control-Allow-Origin: *`), que é o que permite o app em `:5174` falar com a API em `:8090` em desenvolvimento. Em produção o app é same-origin, então CORS não é usado.

Se você restringir o CORS no Admin do PocketBase, inclua `http://localhost:5173` e `http://localhost:5174`.

---

## 5. Regras do PocketBase (leia antes de mexer em `pocketbase/**`)

O template usa **PB 0.40.5** com JSVM. Detalhes completos e comandos prontos:

| Tema | Skill |
|---|---|
| Migrations (criar collection, campo, migrar dados) | `.agents/skills/pocketbase-migrations/SKILL.md` |
| Hooks JS (`pb_hooks/*.pb.js`), `$http`, `$app`, `routerAdd` | `.agents/skills/pocketbase-jsvm/SKILL.md` |
| Routing, SPA, Docker, env vars do PB, erros conhecidos | `.agents/skills/pocketbase-gotchas/SKILL.md` |
| Collections, regras de acesso, tipos TS | `.agents/skills/pocketbase-collections/SKILL.md` |

### Resumo — as 8 regras que mais quebram

1. **Nunca editar uma migration depois de commitada.** O PB registra as aplicadas em `pb_data/_migrations` e não reaplica. Correção = **nova** migration com timestamp maior, idempotente.
2. **PB 0.40+: as opções do field vão direto no field** (`values`, `collectionId`, `maxSelect`, `cascadeDelete`…), **não** dentro de `options: {}`. Errado → `(values: cannot be blank.)`.
3. **Pra adicionar field em collection existente**: `collection.fields.add(new Field({...}))`. Objeto literal → `could not convert [object Object] to core.Field`.
4. **`$app.findAllRecords(coll, { filter })` não existe.** Use `$app.findRecordsByFilter(coll, filtro, sort, limit, offset, params)`. `$app.filter()` também não existe.
5. **`$http.send` devolve `Uint8Array`.** Use `resp.json` ou `toString(resp.body)` — nunca `JSON.parse(resp.body)`.
6. **Não chame `e.next()` em `onRecordAfter*Success/Error`.** Só os hooks `*Execute` precisam. Nos After, é no-op silencioso.
7. **`$app.dao().saveRecord()` dentro de `onRecordAfterCreateSuccess` é descartado** pela transação. Pra alterar o record criado, use `onRecordUpdateExecute` ou um save posterior.
8. **Routing de SPA**: `{path...}` (3 pontos) é catch-all; `{path?}` **não existe**. `$apis.static("pb_public/app", true)` — o `true` (SPA fallback) é obrigatório.

### O hook existente (não quebrar)

`pocketbase/pb_hooks/main.pb.js`:

```js
routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
```

Sem ele, o web app não abre em `/app/...` (a API do PB responde 404 antes do fallback).

### Migrations deste template

`pocketbase/pb_migrations/1794182400_user_profile_fields.js` cria **dois campos** na collection `users`:

- `avatar` — `file` (maxSelect 1, 5 MB, só imagens)
- `emailVisibility` — `bool`

Sem eles, a página `/bio` não funciona (upload de avatar e o toggle de visibilidade). **Toda migration nova vai pro git** — é o que reconstrói o schema em qualquer ambiente.

---

## 6. Workflow obrigatório: validar → commit → push

**REGRA: antes de qualquer `git commit` que toque `apps/web/**`, `apps/landing/**` ou `pocketbase/**`, rode a validação local. Se quebrar, NÃO commite.**

| O que mudou | Validação obrigatória antes do commit |
|---|---|
| `apps/web/**` | `dotnet publish apps/web -c Release -o apps/web/publish` |
| `apps/landing/**` | `dotnet publish apps/landing -c Release -o apps/landing/publish` |
| `Directory.Build.targets` | os dois `dotnet publish` acima (o alvo de cópia roda junto) |
| `pocketbase/pb_migrations/**` | subir o PB local com `pb_data` limpo e conferir o startup |
| `pocketbase/pb_hooks/**` | subir o PB local (hook quebrado = 500 em runtime) e testar a rota |
| `Dockerfile` / `docker-compose.yaml` | `docker build .` |
| `AGENTS.md`, `.agents/**`, `README.md` | nada (docs) |

Ordem de commit sugerida:

```bash
git status --short              # ver o que mudou
# ... rodar as validações acima ...
git add <paths>
git commit -m "tipo(escopo): descrição"
git push
```

**Push não é deploy.** O template não define pipeline de deploy: cada projeto que consome o template decide.

---

## 7. Pegadinhas conhecidas

### Blazor WASM

- **O `<base href>` muda entre dev e prod.** Em dev o app vive na raiz (`http://localhost:5174/`), em prod o PocketBase serve em `/app/`. O `index.html` resolve isso com um script que reescreve o `<base>` conforme a URL — **se você mudar o caminho de montagem, atualize a regex** em `apps/web/wwwroot/index.html`.
- **`dotnet publish` gera `publish/wwwroot/`**, não `publish/`. É esse diretório que vai para `pb_public/`. O `wwwroot/` do publish é o que o browser consome (`_framework/`, `css/`, `js/`).
- **`appsettings.json` é sobrescrito no publish** pelo target `WritePocketBaseUrl` / `WriteWebAppUrl` no `.csproj`. Não edite o valor de prod no fonte — edite a propriedade MSBuild.
- **`InputFile.OnChange` é `EventCallback<InputFileChangeEventArgs>`**, não `IBrowserFile`. O arquivo vem de `args.File`.
- **`IBrowserFile.OpenReadStream` só pode ser chamado uma vez** por arquivo. Leia em memória (`MemoryStream`) e reaproveite os bytes.
- **`TreatWarningsAsErrors` está ligado.** Qualquer warning de compilação quebra o build.
- **`AuthService.InitializeAsync()` roda antes da primeira renderização** (é awaited no `OnInitializedAsync` do `App.razor`). É isso que impede uma rota protegida de redirecionar para o login antes do token ser lido do `localStorage`.
- **`NavigationManager.BaseUri`** já traz a origem — é dele que o `PocketBaseClient` deriva a URL da API quando a config é relativa (`/`). Não precisa de JS interop pra isso.
- ⚠️ **`Uri.TryCreate(x, UriKind.Absolute)` mente no browser-wasm.** O runtime WASM usa semântica de caminho Unix: `"/"` é um caminho absoluto, então `TryCreate("/", UriKind.Absolute)` devolve **true** e o `Uri` vira `file:///`. No desktop (Windows) a mesma chamada devolve **false** — por isso o bug só aparece no navegador. Com a config de produção (`PocketBase:Url = "/"`) o `BaseUrl` virava `file:` e toda requisição quebrava com `System.UriFormatException: net_uri_BadAuthority` ("The hostname could not be parsed") dentro do `HttpClient.PrepareRequestMessage`. **Regra: só trate como URL absoluta o que tem esquema `http`/`https` explícito** (helper `TryGetHttpUri` no `PocketBaseClient`); qualquer outra coisa é caminho relativo e combina com a origem do `NavigationManager.BaseUri`.
- **`HttpClient` NÃO é registrado por default no Blazor WASM.** Sem registro no `Program.cs`, qualquer serviço que o injete quebra o app com `CannotResolveService, System.Net.Http.HttpClient` (aparece como "erro inesperado", não como erro de compilação). E **`AddHttpClient()` não existe aqui** — a extensão vive em `Microsoft.Extensions.Http`, que não é referenciado. Registre na mão: `AddScoped(_ => new HttpClient { BaseAddress = hostOrigin })`.
- **`localStorage` via `IJSRuntime`**, não `pocketbase.storage.get` — esse global é do SDK JS do PocketBase, que não existe nesta página (o cliente é 100% C#). O erro (`'pocketbase' was undefined`) acontece dentro do `Auth.InitializeAsync()` e trava o app antes da primeira renderização.
- **Navegação sempre com caminho RELATIVO** (sem `/` inicial): `Navigation.NavigateTo("bio")` e `<a href="bio">`. Caminho absoluto ignora o `<base href>` e quebra em produção (onde o app mora em `/app/`). Exceção: link pra landing page, que fica na raiz do domínio (`href="/"`).
- **A rota `/` precisa existir.** O Vue fazia `redirect: '/dashboard'`; no Blazor não existe redirect automático — `Pages/Home.razor` faz `NavigateTo("dashboard", replace: true)`. Sem ela, `/app/` mostra a 404 do `Router`.
- **A cópia pro `pb_public` é feita por MSBuild** (alvo `CopyToPocketBase` em `Directory.Build.targets`), não por script. A limpeza usa um **manifesto** (`.published.txt`): só o que o app publicou é apagado — por isso o build da landing não derruba o do web app. Comment XML não pode conter `--` (dá `MSB4024`).
- **O `Dockerfile` tem dois alvos**: `pocketbase` (só o backend — é o que o `docker compose` usa) e `production` (PB + os dois builds). Sem `CMD` no estágio base, o container executa `/bin/sh` e **sai em silêncio com exit 0** — parece que "funcionou" mas o PB nunca subiu.

### Cliente PocketBase

- **Não existe SDK C# "oficial" maduro.** O template traz um cliente próprio (`PocketBaseClient.cs`) sobre `HttpClient` + `System.Text.Json` — ~200 linhas, zero dependência, sem risco de breaking change.
- **Erro do PB vem em JSON**: `{ code, message, data: { campo: { message } } }`. `PocketBaseException.FieldErrors` expõe o `data` já parseado.
- **Header de auth**: `Authorization: <token>` — **token cru, sem prefixo**. O PB 0.40 não aceita `Authorization: Token <token>` (devolve 404 silencioso no GET do registro). É o mesmo formato do SDK JS.
- **Token no navegador**: chave `pocketbase_auth` no `localStorage`. Para invalidar, troque a chave ou limpe o storage.

### BeerCSS

- **BeerCSS v5 é CSS puro** — sem build step. O `wwwroot/beercss/` tem `beer.min.css` + a fonte Material Symbols (para os `<i>nome_do_icone</i>`).
- **O `beer.min.js` NÃO está vendorizado** — só é necessário para componentes interativos do BeerCSS (tabs, menus, dialogs). O app atual não usa nenhum. Se precisar, baixe de `https://cdn.jsdelivr.net/npm/beercss@5.0.3/dist/cdn/beer.min.js` e **não** carregue junto com o `beer.min.css` (o JS injeta os módulos de CSS de novo).
- **Card é `<article>`**, não `.card`. Os estilos `.card-header` / `.card-body` são do app (`css/app.css`), não do BeerCSS.
- **Campo com label flutuante** precisa do `placeholder=" "` no input (o CSS usa `:placeholder-shown`).
- **Ícones**: `<i>rocket_launch</i>` — o nome do ícone é o texto dentro da tag.

### Tema

- **Cores flat, sem gradiente.** No máximo 1 primária + 1 accent, ambas sólidas. Os tokens Material 3 são sobrescritos em `apps/*/wwwroot/css/app.css` (bloco `:root, body.light` + bloco `body.dark` via `prefers-color-scheme`).

### PocketBase

- **PB em container não hot-reloada hook** se o volume montar por cima: reinicie o processo.
- **`pb_data/types.d.ts` é gerado pelo PB em runtime.** Não edite à mão — rode o PB e deixe ele regenerar (o arquivo é versionado no git).
- **`pb_data/data.db` é o estado inteiro.** Versionar `pb_migrations/` reconstrói o schema; os **dados** só voltam via backup.

---

## 8. Nunca commitar

- `apps/*/publish/`, `apps/*/bin/`, `apps/*/obj/` (já no `.gitignore`)
- `pocketbase/pb_data/` (exceto `.gitkeep` e `types.d.ts`) — **dados de usuário**
- Senha/credencial do admin do PocketBase
- Tokens de infra (deploy, registry, API keys). Use placeholder (`<token>`) na documentação e leia do ambiente em runtime.

---

## 9. Skills disponíveis

`.agents/skills/` — carregadas automaticamente por agentes compatíveis (OpenCode lê `.agents/skills/`, `.claude/skills/` e `.opencode/skills/`).

| Skill | Quando usar |
|---|---|
| `pocketbase-migrations` | criar/alterar collection, campo, migration de dados |
| `pocketbase-jsvm` | escrever/depurar hook em `pb_hooks/`, `$http`, `$app`, `routerAdd` |
| `pocketbase-gotchas` | routing, SPA, Docker, env vars do PB, erros conhecidos |
| `pocketbase-collections` | criar collection, regras de acesso, hooks de evento |
| `pre-deploy-validation` | validar antes de commitar/pushar (build, migration, docker) |
| `blazor-service` | criar/editar um serviço C# (Auth, Profile, PocketBase client) |
| `blazor-auth-guard` | proteger rota, redirecionar, gerenciar sessão |
| `blazor-page` | criar página/componente Razor com BeerCSS |
| `beercss-blazor` | usar componentes do BeerCSS dentro do Blazor |
| `blazor-config` | `appsettings.json`, URLs de dev/prod, publish |

Índice com exemplos de prompt: `.agents/skills/README.md`.

---

## 10. Referência rápido de comandos

```bash
# PocketBase local
docker compose up -d

# validar migrations do zero
docker compose down -v              # remove o volume do banco
docker compose up -d                # o PB aplica as migrations no boot

# build completo (copia pro pb_public)
dotnet publish apps/landing -c Release -o apps/landing/publish
dotnet publish apps/web     -c Release -o apps/web/publish

# imagem de produção
docker build -t pocketbase-blazor-beercss .
```
