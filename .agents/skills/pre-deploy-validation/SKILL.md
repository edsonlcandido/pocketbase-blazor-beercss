---
name: pre-deploy-validation
description: Rodar as validações locais ANTES de qualquer commit/push que toque migrations, hooks ou o código dos apps Blazor neste template. Três checks: (1) PocketBase local com pb_data limpo pra confirmar que a migration aplica limpa, (2) dotnet publish dos apps tocados pra pegar erro de compilação/build, (3) docker build quando o Dockerfile mudou. Use quando o pedido mencionar "validar antes de deploy", "rodar local antes", "verificar migration", "build", "pre-deploy", "commita e sobe", "deploy", ou quando o commit for iminente.
---

# Pre-deploy validation (validação local antes de commitar/pushar)

Publicar código quebrado custa minutos de build em produção e a impressão de que "não foi nada". **Antes de qualquer `git commit` que toque `pocketbase/**` ou `apps/**`, rode os checks abaixo.** Se quebrar, **NÃO commite**.

Os checks são proporcionais ao que mudou — não rode tudo sempre.

| O que mudou | Check |
|---|---|
| `apps/web/**` | Check 2 — `npm run build:web` |
| `apps/landing/**` | Check 2 — `npm run build:landing` |
| `pocketbase/pb_migrations/**` | Check 1 — PB local com `pb_data` limpo |
| `pocketbase/pb_hooks/**` | Check 1 — PB local (hook quebrado = 500 em runtime) |
| `Dockerfile` / `docker-compose.yaml` | Check 3 — `docker build .` |
| `AGENTS.md`, `.agents/**`, `README.md` | nada (docs) |

---

## Check 1 — migration/hook do PocketBase roda limpo

```bash
# 1) Limpa o pb_data (estado limpo: garante que a migration roda do zero)
rm -rf pocketbase/pb_data          # Linux/macOS
# Windows: use a lixeira do runtime (mavis-trash) — NUNCA "Remove-Item -Recurse -Force"

# 2) Sobe o PB. Ele aplica TODAS as migrations de pb_migrations/ no startup.
npm run dev:pb
```

Aguarde o startup e olhe o stdout:

- ✅ **Sinal verde**: `Server started at http://127.0.0.1:8090` e **nenhuma** linha `failed to apply migration`.
- ❌ **Sinal vermelho**: `Error: failed to apply migration 1794182400_x.js: <erro>` → **NÃO commite**. Corrija a migration e volte do passo 1.

> O PB pode continuar rodando mesmo com migration falhada. Olhe **a linha de erro**, não só a de "server started".

### Verificações de sanidade (SQLite)

```bash
sqlite3 pocketbase/pb_data/data.db "SELECT file FROM _migrations ORDER BY file"
sqlite3 pocketbase/pb_data/data.db "PRAGMA table_info(users)"
sqlite3 pocketbase/pb_data/data.db "SELECT name FROM _collections ORDER BY name"
```

**Pós-checks** (se a migration mexeu em dados):

```bash
sqlite3 pocketbase/pb_data/data.db "SELECT COUNT(*) FROM posts"
```

### Confirme o endpoint pela API (rápido e revelador)

```bash
# health
curl -sS -m 10 http://127.0.0.1:8090/api/health

# o campo que a migration criou apareceu no registro?
curl -sS -m 10 http://127.0.0.1:8090/api/collections/users/records \
  -H "Authorization: <token>"
```

> O header de auth do PB 0.40 é `Authorization: <token>` **cru** (sem prefixo `Token `).

Pare o PB local (Ctrl+C) quando terminar — ele não é um daemon que deve ficar rodando.

### Quando pular

- Mudou só `apps/**` → pule o Check 1.
- Mudou só hook (sem migration) → o PB recarrega hook sozinho; basta subir e testar a rota afetada.

---

## Check 2 — build dos apps tocados

```bash
# apps/web (Blazor WebAssembly)
npm run build:web

# apps/landing (se você mexeu)
npm run build:landing
```

Ou direto no app:

```bash
dotnet publish apps/web -c Release -o apps/web/publish
dotnet publish apps/landing -c Release -o apps/landing/publish
```

- ✅ **Sinal verde**: exit code 0, sem `error`/`warning`.
- ❌ **Sinais vermelhos** (bloqueiam o commit):
  - `error CSxxxx` — erro de compilação C#.
  - **Qualquer warning** — `TreatWarningsAsErrors` está ligado nos dois `.csproj`, então warning **quebra** o build.
  - `MSB4044` — target MSBuild com parâmetro obrigatório faltando (o `WriteLinesToFile` precisa de `File`).
  - exit code ≠ 0.

### Valide o conteúdo publicado

```bash
# a config de PROD foi injetada?
cat apps/web/publish/wwwroot/appsettings.json
# esperado: { "PocketBase": { "Url": "/" } }

# o framework do Blazor foi pro publish?
ls apps/web/publish/wwwroot/_framework | head

# o build foi copiado pro pb_public?
ls pocketbase/pb_public/app | head
```

> ⚠️ No .NET 10 **não existe `blazor.boot.json`** — o boot manifest é embutido no `blazor.webassembly.js`. Não procure o arquivo.

### Quando pular

- Mudou só `pocketbase/**` → pule o Check 2.
- Mudou só docs → pule tudo.

---

## Check 3 — imagem Docker

```bash
docker build -t pocketbase-blazor-beercss:check .
```

- ✅ sai com exit 0.
- ❌ erro de build (falta de arquivo copiado no Dockerfile, versão do PB inválida no `ARG POCKETBASE_VERSION`, `dotnet publish` falhando dentro do build).

Rodar a imagem e validar de ponta a ponta:

```bash
docker compose up -d
curl -sS -m 10 http://localhost:8090/api/health    # deve responder
curl -sS -m 10 http://localhost:8090/ | head -20      # landing
curl -sS -m 10 http://localhost:8090/app/ | head -20   # web app
docker compose down
```

---

## Atalho — checklist de commit

```bash
# 1) O que mudou?
git status --short

# 2) Rodar só os checks aplicáveis
#    - tocou apps/web      → npm run build:web
#    - tocou apps/landing  → npm run build:landing
#    - tocou pb_migrations → Check 1 (PB local + sqlite3)
#    - tocou Dockerfile    → docker build .

# 3) Tudo verde?
git add <paths>
git commit -m "tipo(escopo): descrição"
git push
```

---

## Erros que já custaram rollback (e são detectados aqui)

1. **Field com opções aninhadas em `options: {}`** (sintaxe PB 0.22) → `(values: cannot be blank.)`. A migration não aplica.
2. **`findAllRecords(coll, { filter })`** (não existe no PB 0.40+) → `Object has no member 'filter'`. Migration aplica pela metade e deixa estado inconsistente.
3. **`Authorization: Token <token>`** → o PB 0.40 devolve 404 silencioso. O header correto é `Authorization: <token>` cru.
4. **Endpoint sem `/records`** (`/api/collections/users/{id}`) → 404. CRUD usa `/api/collections/{col}/records/{id}`.
5. **`InputFile.OnChange` tipado como `IBrowserFile`** → não compila. É `InputFileChangeEventArgs`.
6. **`OpenReadStream` chamado duas vezes** no mesmo `IBrowserFile` → `InvalidOperationException` em runtime. Leia em memória e reutilize os bytes.
7. **`TreatWarningsAsErrors`** → qualquer warning quebra o build.

Todos esses aparecem **localmente**, em segundos — e só apareceriam em produção depois do build completo.

---

## Quando invocar esta skill

- O pedido é "commita e sobe" / "manda pra prod" / "deploy" / "publica".
- Antes de qualquer `git push` que toque `pocketbase/pb_migrations/*`, `pocketbase/pb_hooks/*`, `apps/web/**` ou `apps/landing/**`.
- Quando você se pega fazendo `git commit` sem ter rodado os checks — confira `git status` para ver se há migration ou front pendente.

## Skills relacionadas

- `.agents/skills/pocketbase-migrations/SKILL.md` — sintaxe de migration (fields, timestamps, idempotência)
- `.agents/skills/pocketbase-jsvm/SKILL.md` — regras de hooks JS no PB 0.40+
- `.agents/skills/blazor-config/SKILL.md` — config de dev/prod e publish
- `AGENTS.md` (raiz) — contexto do projeto e workflow de commit
