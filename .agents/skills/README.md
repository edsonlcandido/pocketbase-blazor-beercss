# Skills do template

Instruções estruturadas pra agentes de IA trabalharem com este template (PocketBase + Blazor WebAssembly + BeerCSS).

Cada skill é um `SKILL.md` com frontmatter:

```yaml
---
name: pocketbase-migrations
description: Quando o agente deve carregar esta skill. É a única coisa que ele vê antes de decidir.
---
```

O `description` é o gatilho: **descreva o pedido do usuário**, não o conteúdo da skill.

## Como são descobertas

O diretório `.agents/skills/` é lido automaticamente por:

| Agente | Lê `.agents/skills/` |
|---|---|
| OpenCode | ✅ (compatibilidade de projeto) |
| Claude Code | ✅ (`.claude/skills/`) |
| Codex / Cursor / Windsurf | ✅ (convenção `AGENTS.md`) |

Copie pra `.claude/skills/` se quiser que o Claude Code enxergue:

```bash
cp -r .agents/skills/* .claude/skills/
```

O contexto geral do projeto (estrutura, comandos, regras do PB, workflow de commit) está em **`AGENTS.md`** na raiz — leia ele junto.

## Índice

### 🧱 PocketBase

| Skill | Quando usar |
|---|---|
| `pocketbase-migrations` | Criar/alterar collection, campo, migration de dados, `pb_migrations/` |
| `pocketbase-jsvm` | Escrever/depurar hook em `pb_hooks/`, `$http`, `$app`, `routerAdd`, `e.next()` |
| `pocketbase-collections` | Criar collection, regras de acesso, índices, hooks de evento, tipos C# |
| `pocketbase-gotchas` | Routing de SPA, `$apis.static`, wildcard `{path...}`, Dockerfile do PB, env do PB |

### ✅ Validação

| Skill | Quando usar |
|---|---|
| `pre-deploy-validation` | Antes de commitar/pushar: rodar build, subir PB local, checar migration |

### 🎨 Frontend (Blazor + BeerCSS)

| Skill | Quando usar |
|---|---|
| `blazor-service` | Criar/editar serviço C#, cliente do PocketBase, DI, estado compartilhado |
| `blazor-auth-guard` | Proteger rota, redirecionar, sessão, token, logout |
| `blazor-page` | Criar página/componente Razor, formulário, validação, upload |
| `beercss-blazor` | Classes do BeerCSS, grid, campo, botão, card, ícone, tema flat |
| `blazor-config` | `appsettings.json`, URLs de dev/prod, publish, `<base href>`, CORS |

## Exemplos de prompt que disparam cada skill

| Você fala | Skill acionada |
|---|---|
| "cria uma collection de pedidos" | `pocketbase-collections` + `pocketbase-migrations` |
| "adiciona um campo `status` na collection de posts" | `pocketbase-migrations` |
| "faz um hook que dispara quando o post é criado" | `pocketbase-jsvm` |
| "a SPA dá 404 em /app/qualquer-coisa" | `pocketbase-gotchas` |
| "valida antes de commitar" | `pre-deploy-validation` |
| "cria um serviço de carrinho que fala com o PocketBase" | `blazor-service` |
| "cria a rota /config só pra admin" | `blazor-auth-guard` |
| "faz uma tela de cadastro com validação" | `blazor-page` |
| "faz um botão com loading usando BeerCSS" | `beercss-blazor` |
| "aponta o front pra outro PocketBase" | `blazor-config` |

## Workflow sugerido

1. **Descreva a tarefa** em linguagem natural — as skills são carregadas sozinhas.
2. **PocketBase**: comece por `pocketbase-collections` (o que é uma collection, permissões) e `pocketbase-migrations` (como aplicar no schema).
3. **Frontend**: `blazor-config` → `blazor-service` → `blazor-auth-guard` → `blazor-page` → `beercss-blazor`.
4. **Antes de commitar**: `pre-deploy-validation`.

## Criando uma skill nova

1. Crie `skills/<nome-em-kebab-case>/SKILL.md` (ou direto em `.agents/skills/<nome>/SKILL.md`).
2. Frontmatter com `name` + `description` (o `description` precisa conter os **gatilhos em linguagem do usuário**).
3. Corpo em Markdown: comandos prontos pra copiar, snippets do template, checklist, anti-padrões.
4. Adicione entrada no índice deste README.
5. Referencie as outras skills por caminho (`.agents/skills/<nome>/SKILL.md`).

## Licença

Mesma do template (MIT).
