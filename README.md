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
| [Node.js](https://nodejs.org) | >= 20.19.0 | só para orquestrar os comandos (`npm run ...`) |
| [Docker](https://docs.docker.com/get-docker/) | qualquer | imagem de produção (opcional) |

## ⚡ Começando

```bash
# 1. instala a única dependência npm (o concurrently)
npm install

# 2. sobe tudo: PocketBase + landing + web app
npm run dev
```

| Serviço | URL |
|---|---|
| Landing page | http://localhost:5173 |
| Web app (SPA logada) | http://localhost:5174 |
| PocketBase API | http://localhost:8090/api/ |
| Admin do PocketBase | http://localhost:8090/_/ |

Na primeira vez, o `npm run dev` baixa o binário do PocketBase automaticamente.

> **Crie o primeiro usuário**: abra http://localhost:8090/_/ e complete o instalador do superuser. Depois, em http://localhost:5174, use **Criar uma nova conta** — a collection `users` já vem com os campos `avatar` e `emailVisibility` (migration inicial inclusa).

## 📦 Scripts

```bash
npm run dev              # PB :8090 + landing :5173 + web :5174
npm run dev:pb           # só PocketBase
npm run dev:landing      # só a landing (dotnet watch)
npm run dev:web          # só o web app (dotnet watch)

npm run build            # build:landing → copy:landing → build:web → copy:app
npm run build:landing    # dotnet publish da landing
npm run build:web        # dotnet publish do web app
npm run copy:landing     # apps/landing/publish/wwwroot → pocketbase/pb_public
npm run copy:app         # apps/web/publish/wwwroot → pocketbase/pb_public/app

npm run preview          # sobe o PocketBase já com os builds copiados
```

Sem Node? Os comandos diretos são:

```bash
node scripts/pocketbase.js
dotnet watch run --project apps/landing
dotnet watch run --project apps/web
dotnet publish apps/web -c Release -o apps/web/publish
node scripts/copy-app.js
```

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
├── scripts/              # pocketbase.js, copy-landing.js, copy-app.js
├── Dockerfile
├── docker-compose.yaml
├── AGENTS.md             # contexto operacional para agentes de IA
└── package.json
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

```bash
docker build -t pocketbase-blazor-beercss .
docker compose up -d
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
