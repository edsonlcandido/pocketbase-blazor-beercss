# PocketBase + Blazor WebAssembly + BeerCSS Template

Template completo para projetos com **PocketBase** + **Blazor WebAssembly** + **BeerCSS** em arquitetura monorepo.

Derivado do [pocketbase-vue-tailwind-template](https://github.com/edsonlcandido/pocketbase-vue-tailwind-template) — mesma arquitetura (PocketBase servindo o app e a API na mesma origem), com o frontend reescrito em C#/Blazor e o Tailwind trocado pelo BeerCSS.

## 🚀 Características

- **Backend completo** — PocketBase 0.40.5 com SQLite embutido: API REST, auth, realtime e admin UI em um único executável.
- **Blazor WebAssembly (.NET 10)** — C# tipado, componentização no lado do cliente, hot reload no `dotnet watch`.
- **BeerCSS 5.0.3** — Material Design 3, **sem build step** e sem JavaScript obrigatório.
- **Monorepo com dois apps** — landing page pública (`/`) + SPA logada (`/app/`).
- **Autenticação pronta** — login, cadastro, sessão persistente no `localStorage` e rotas protegidas.
- **Perfil de usuário** — editar nome, senha, visibilidade do e-mail e enviar foto de perfil.
- **Cliente PocketBase em C#** — camada fina e tipada sobre `HttpClient`, **sem SDK externo**.
- **Tema flat** — no máximo 1 cor primária + 1 accent, ambas **sólidas** (sem gradiente).
- **Docker de arquivo único** — uma imagem com o PocketBase e os dois builds.
- **Migrations versionadas** — o schema é reconstruído do zero em qualquer ambiente.
- **AGENTS.md + skills** — contexto operacional para agentes de IA.

## 📋 Pré-requisitos

| Ferramenta | Versão | Para quê |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10 (8/6 também funcionam) | build e dev dos apps |
| [Docker](https://docs.docker.com/get-docker/) | qualquer | PocketBase local + imagem de produção |

> **Sem Node, sem npm.** O projeto inteiro usa só o .NET SDK (e Docker, opcional). Não há `package.json`, `node_modules` nem script JS.

## ⚡ Começando

```bash
# 1. sobe o PocketBase (API + admin + build estático) em :8090
docker compose up -d

# 2. em outro terminal, a landing com hot reload em :5173
dotnet watch run --project apps/landing

# 3. e em mais um, o web app com hot reload em :5174
dotnet watch run --project apps/web
```

| Serviço | URL |
|---|---|
| Landing page | http://localhost:5173 |
| Web app (SPA logada) | http://localhost:5174 |
| PocketBase API | http://localhost:8090/api/ |
| Admin do PocketBase | http://localhost:8090/_/ |

> **Crie o primeiro usuário**: abra http://localhost:8090/_/ e complete o instalador do superuser. Depois, em http://localhost:5174, use **Criar uma nova conta** — a collection `users` já vem com os campos `avatar` e `emailVisibility` (migration inicial inclusa).

### Sem Docker?

Baixe o binário do PocketBase 0.40.5 e rode direto:

```bash
# Linux/macOS
wget https://github.com/pocketbase/pocketbase/releases/download/v0.40.5/pocketbase_0.40.5_linux_amd64.zip
unzip pocketbase_0.40.5_linux_amd64.zip && chmod +x pocketbase
./pocketbase serve --http=127.0.0.1:8090

# Windows (PowerShell)
Invoke-WebRequest https://github.com/pocketbase/pocketbase/releases/download/v0.40.5/pocketbase_0.40.5_windows_amd64.zip -OutFile pb.zip
Expand-Archive pb.zip -DestinationPath pocketbase -Force
.\pocketbase\pocketbase.exe serve --http=127.0.0.1:8090
```

Rode o binário **de dentro da pasta `pocketbase/`** — é de lá que ele lê `pb_hooks/`, `pb_migrations/`, `pb_data/` e `pb_public/`.

## 📦 Comandos

```bash
# desenvolvimento (hot reload)
dotnet watch run --project apps/landing        # :5173
dotnet watch run --project apps/web            # :5174
docker compose up -d                          # PocketBase :8090

# build (o MSBuild já copia o resultado pro pocketbase/pb_public)
dotnet publish apps/landing -c Release -o apps/landing/publish
dotnet publish apps/web     -c Release -o apps/web/publish

# build só para validar a compilação (sem copiar pro pb_public)
dotnet publish apps/web -c Release -p:SkipPocketBaseCopy=true

# preview: o PocketBase serve o build em http://localhost:8090/
docker compose up -d
```

### Como o build chega no PocketBase

O alvo `CopyToPocketBase` (em `Directory.Build.targets`, na raiz) roda depois do `dotnet publish` e copia `publish/wwwroot` para a pasta pública do PocketBase:

| App | Destino | Servido em |
|---|---|---|
| `apps/landing` | `pocketbase/pb_public` | `/` |
| `apps/web` | `pocketbase/pb_public/app` | `/app/` |

A limpeza usa um manifesto (`.published.txt`) com a lista de arquivos da última publicação — **só o que o app publicou é apagado**, então o build da landing nunca derruba o build do web app.

## 🗂️ Estrutura

```
pocketbase-blazor-beercss/
├── apps/
│   ├── landing/          # landing page (Blazor WASM + BeerCSS)
│   └── web/              # SPA logada (login, dashboard, perfil)
├── pocketbase/
│   ├── pb_hooks/         # hooks JS (routing de SPA)
│   ├── pb_migrations/    # migrations JS (versionadas no git)
│   ├── pb_data/          # estado do PocketBase (gitignored)
│   └── pb_public/        # builds servidos pelo PB (/ e /app)
├── Directory.Build.targets   # alvo CopyToPocketBase (copia o build pro pb_public)
├── Dockerfile            # alvos: pocketbase (só o backend) e production (tudo)
├── docker-compose.yaml   # PocketBase local
├── AGENTS.md             # contexto operacional para agentes de IA
└── README.md
```

## ⚙️ Configuração

A configuração fica em `apps/<app>/wwwroot/appsettings.json` e é lida **em runtime**.

| Variável | App | Dev | Prod |
|---|---|---|---|
| `PocketBase:Url` | web | `http://localhost:8090` | `/` (mesma origem) |
| `WebApp:Url` | landing | `http://localhost:5174` | `/app/` |

O valor de produção é injetado no `dotnet publish`:

```bash
dotnet publish apps/web -c Release -o apps/web/publish -p:PocketBaseUrl=https://api.exemplo.com
dotnet publish apps/landing -c Release -o apps/landing/publish -p:WebAppUrl=https://app.exemplo.com
```

> ⚠️ `appsettings.json` é servido publicamente — **nunca** coloque segredo ali.

## 🐳 Docker

O `Dockerfile` tem dois alvos:

| Alvo | O que é | Quando usar |
|---|---|---|
| `pocketbase` | só o binário do PocketBase (Alpine) | backend local — é o que o `docker compose` usa |
| `production` | PocketBase + os dois builds estáticos | imagem de deploy |

```bash
# backend local (o compose monta pb_public, pb_hooks e pb_migrations)
docker compose up -d

# imagem de produção (PocketBase servindo a landing, o app e a API)
docker build -t pocketbase-blazor-beercss .
docker run -p 8090:8090 -v pocketbase-data:/app/pb_data pocketbase-blazor-beercss
```

A imagem final é um Alpine com o binário do PocketBase + os dois builds estáticos. Os hooks e as migrations vão junto — o schema é reconstruído no boot.

## 🎨 Tema

As cores do Material 3 do BeerCSS são sobrescritas em `apps/*/wwwroot/css/app.css`. A regra do template: **1 primária + 1 accent, ambas sólidas, sem gradiente**.

```css
:root,
body.light {
    --primary: #1d4ed8;          /* azul — primária */
    --on-primary: #ffffff;
    --secondary: #0f766e;        /* teal — accent */
    --on-secondary: #ffffff;
    /* ... */
}
```

O bloco `body.dark` (via `prefers-color-scheme`) cobre o tema escuro.

## 🧩 BeerCSS

O BeerCSS v5 é **CSS puro** — o `wwwroot/beercss/` de cada app tem o `beer.min.css` e a fonte Material Symbols.

```html
<!-- botão -->
<button class="button"><i>rocket_launch</i> Enviar</button>

<!-- campo com label flutuante -->
<div class="field label border">
    <input type="email" placeholder=" " />
    <label>E-mail</label>
</div>

<!-- grid responsivo -->
<div class="grid">
    <div class="s12 m6 l4">...</div>
</div>
```

O `beer.min.js` **não** está vendorizado — só é necessário para componentes interativos (tabs, menus, dialogs).

## 🧠 Contexto para agentes de IA

O [`AGENTS.md`](./AGENTS.md) documenta a arquitetura, os comandos, as regras do PocketBase que mais quebram e as pegadinhas do Blazor/BeerCSS. As skills em [`.agents/skills/`](./.agents/skills) trazem os comandos prontos.

## 📄 Licença

MIT. Derivado de [pocketbase-vue-tailwind-template](https://github.com/edsonlcandido/pocketbase-vue-tailwind-template) (MIT).

## 🔗 Agradecimentos

- [PocketBase](https://pocketbase.io/) — backend incrível
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) — C# no navegador
- [BeerCSS](https://www.beercss.com/) — CSS Material Design 3 sem build step
