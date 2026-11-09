---
name: pocketbase-migrations
description: Criar, verificar e depurar migrations JS do PocketBase neste template (pocketbase/pb_migrations/*.js, PB 0.40.5). Cobre as 3 regras que sempre quebram no primeiro deploy: (1) nunca editar uma migration já commitada — correção vai em NOVA migration com timestamp maior, (2) no PB 0.40+ as opções do field vão DIRETO no field (não dentro de `options: {}`), (3) adicionar field em collection existente exige a classe `Field`. Use quando o pedido mencionar migration, schema, nova collection, novo campo, pb_migrations, types.d.ts, alterar tabela.
---

# PocketBase — Migrations JS (PB 0.40.5)

Migrations deste template ficam em `pocketbase/pb_migrations/*.js` e são aplicadas **automaticamente pelo PocketBase no boot** — em dev e em qualquer ambiente que suba o PB. É o que reconstrói o schema a partir do zero.

Três regras abaixo já custaram rollback de deploy em projetos reais. Não são着她的 vaidade — são load-bearing.

---

## Regra 1 — nunca editar uma migration depois de commitada

O PB registra as migrations aplicadas em `pb_data/_migrations` (tabela SQLite). Depois que o arquivo foi commitado **e rodou**, ele está congelado:

- Rodou com sucesso → entrada em `_migrations`, não roda de novo. Editar o arquivo **não muda nada**.
- Falhou e deixou estado parcial → a entrada pode ou não ter sido gravada, dependendo de onde quebrou. **A única correção segura é uma migration nova.**

```bash
# 1) Gera um timestamp novo (Unix epoch, UTC) — tem que ser MAIOR que o último
date +%s
# PowerShell:
[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

# 2) Cria o arquivo com a correção, de forma IDEMPOTENTE
```

A nova migration **não substitui** a antiga: as duas ficam em `pb_migrations/`. A antiga vira tombstone (o PB pula porque já está em `_migrations`, ou porque nunca chegou a ser aplicada).

> Exceção: se a migration **nunca foi commitada** e nem rodou em nenhum lugar, pode simplesmente apagar o arquivo — isso é "desfazer uma mudança local", não "editar uma migration".

## Regra 2 — no PB 0.40+ as opções do field vão direto no field

Sintaxe antiga (PB < 0.22) vs. sintaxe atual:

```js
// ❌ ERRADO (PB 0.22): opções aninhadas
{ name: "status", type: "select", required: false,
  options: { maxSelect: 1, values: ["draft", "published"] } }
//   ↑ em produção: "(values: cannot be blank.)"

// ✅ CERTO (PB 0.40+): opções no próprio field
{ name: "status", type: "select", required: false,
  values: ["draft", "published"], maxSelect: 1 }
```

| Tipo | Opções (no field, **nunca** dentro de `options`) |
|---|---|
| `text` | `max`, `min`, `pattern`, `required` |
| `editor` | `maxSize`, `required` |
| `number` | `min`, `max`, `onlyInt`, `required` |
| `bool` | `required` |
| `select` | `values: string[]`, `maxSelect`, `required` |
| `relation` | `collectionId`, `cascadeDelete`, `minSelect`, `maxSelect`, `required` |
| `url` | `exceptDomains`, `onlyDomains`, `required` |
| `file` | `maxSelect`, `maxSize`, `mimeTypes`, `required` |
| `json` | `maxSize`, `required` |

## Regra 3 — pra adicionar field em collection existente, use a classe `Field`

```js
// ❌ ERRADO: objeto literal
collection.fields.add({ name: "name", type: "text", max: 60 })
//   ↑ "could not convert [object Object] to core.Field"

// ✅ CERTO
collection.fields.add(
  new Field({ name: "name", type: "text", max: 60, required: false })
)
```

Atalho válido: `collection.fields.add("name")` cria um `TextField` com defaults (útil quando for ajustar as props depois).

---

## Consultas de records dentro de migration

`findAllRecords(coll, { filter })` e `$app.filter(...)` **não existem** no PB 0.40+:

```js
// 1) Sem filtro: traz tudo
const all = app.findAllRecords("posts")

// 2) Com dbx expressions (variádico, não objeto):
app.findAllRecords("posts", dbx.NewExp("author = {:aid}", { aid: "abc" }))
app.findAllRecords("posts", dbx.HashExp({ published: true }))

// 3) Com string filter + params no final
app.findRecordsByFilter("posts",
  "author = {:aid} && published = true",
  "",     // sort (vazio = ordem padrão)
  500,    // limit
  0,      // offset
  { aid: "abc" }   // dbx.Params
)
```

**Sintoma do erro:** query retorna `[]` silenciosamente (sem throw) ou `TypeError: Object has no member 'filter'`. A migration "aplica" mas não migra os dados. **Confirme com `sqlite3` que os dados foram migrados** (ver Check 1 da skill `pre-deploy-validation`).

---

## Timestamp do arquivo

Nome do arquivo = **Unix epoch em segundos**. Precisa ser **estritamente maior** que o da última migration.

```bash
ls pocketbase/pb_migrations/ | sort -n | tail -1     # ver o maior
date +%s                                          # gerar o próximo
```

> Convenção alternativa (usada em alguns projetos): epoch deslocado pro fuso local (BRT-as-UTC = `epoch - 3*3600`). O que importa é: **um número, único, crescente**.

---

## Esqueleto: migration idempotente completa

```js
/// <reference path="../pb_data/types.d.ts" />

// Adiciona `slug` em `posts` e cria a collection `tags`.
// Idempotente: pode rodar várias vezes sem quebrar.
migrate((app) => {
  // 1) Field novo em collection existente
  const posts = app.findCollectionByNameOrId("posts")
  if (!posts.fields.getByName("slug")) {
    posts.fields.add(
      new Field({ name: "slug", type: "text", max: 120, required: false })
    )
    app.save(posts)
  }

  // 2) Collection nova (try/catch — pode já existir)
  let tags
  try {
    tags = app.findCollectionByNameOrId("tags")
  } catch {
    tags = null
  }
  if (!tags) {
    tags = new Collection({
      name: "tags",
      type: "base",
      listRule: "@request.auth.id != ''",
      viewRule: "@request.auth.id != ''",
      createRule: "@request.auth.id != ''",
      updateRule: "@request.auth.id != ''",
      deleteRule: "@request.auth.id != ''",
      fields: [
        { name: "name", type: "text", max: 60, required: true },
        { name: "post", type: "relation", required: true,
          collectionId: posts.id, cascadeDelete: true,
          minSelect: 1, maxSelect: 1 },
      ],
      indexes: [
        "CREATE INDEX idx_tags_post ON tags (post)",
      ],
    })
    app.save(tags)
  }

  // 3) Migrar dados legados (idempotente)
  for (const r of app.findAllRecords("posts")) {
    const oldName = r.getString("tag_name")
    if (!oldName) continue
    const already = app.findRecordsByFilter("tags",
      "post = {:pid} && name = {:name}", "", 1, 0,
      { pid: r.id, name: oldName })
    if (already.length > 0) continue
    app.save(new Record(tags, { name: oldName, post: r.id }))
  }
}, (app) => {
  // down — best effort
  try { app.delete(app.findCollectionByNameOrId("tags")) } catch {}
  try {
    const posts = app.findCollectionByNameOrId("posts")
    if (posts.fields.getByName("slug")) {
      posts.fields.removeByName("slug")
      app.save(posts)
    }
  } catch {}
})
```

**Regras de uma migration idempotente:**

1. Busca a collection com `try/catch` (ou `if (!col)`) antes de criar.
2. Checa o field antes de adicionar (`if (!col.fields.getByName("x"))`).
3. Migra dado legado checando se o registro novo já existe.
4. `down` nunca derruba nada que possa existir em produção: envolve em `try/catch`.

---

## Testar a migration ANTES de commitar

Detalhe do roteiro: `.agents/skills/pre-deploy-validation/SKILL.md` (Check 1). Resumo:

```bash
# 1) PB local (o script baixa o binário 0.40.5 se não existir)
npm run dev:pb
# ou: cd pocketbase && ./pocketbase serve --http=127.0.0.1:8090

# 2) Rodar do zero: limpa o pb_data (garante que a migration roda do início)
rm -rf pocketbase/pb_data          # Linux/macOS
# PowerShell: Remove-Item -Recurse -Force pocketbase\pb_data

# 3) Inspecionar o startup
#    ✅ "Server started at http://127.0.0.1:8090" e NENHUM "failed to apply migration"
#    ❌ "Error: failed to apply migration 1234_x.js: <erro>"  → NÃO commitar

# 4) Conferir o resultado no SQLite
sqlite3 pocketbase/pb_data/data.db "SELECT file FROM _migrations ORDER BY file"
sqlite3 pocketbase/pb_data/data.db "PRAGMA table_info(posts)"
sqlite3 pocketbase/pb_data/data.db "SELECT name FROM _collections ORDER BY name"
```

> O PB pode **continuar rodando** mesmo com migration falhada. Olhe sempre a linha de erro, não só a linha de "server started".

---

## Tabela de erros → causa → correção

| Erro no log / banco | Causa | Correção |
|---|---|---|
| `could not convert [object Object] to core.Field` | `fields.add({...})` com objeto literal | `fields.add(new Field({...}))` |
| `(collectionId: cannot be blank.)` | `options: { collectionId }` (sintaxe PB 0.22) | `collectionId` direto no field |
| `(values: cannot be blank.)` | `options: { values: [...] }` | `values` direto no field |
| `(maxSize: cannot be blank.)` | mesma raiz, campo `file`/`json` | mover a opção pro field |
| `TypeError: Object has no member 'filter'` | `findAllRecords(coll, { filter })` / `$app.filter()` | `findRecordsByFilter(coll, filtro, sort, limit, offset, params)` |
| `(min: cannot be blank.)` do nada | migration anterior rodou 2x ou foi rollback | conferir `SELECT file FROM _migrations` |
| `notNull constraint failed: <tabela>.<col>` | o `down` tentou dropar coluna com dados | no `down`, limpar os dados antes de dropar |
| migration "aplica" mas nada mudou | filtro inexistente retornou `[]` silencioso | validar com `sqlite3` + `SELECT COUNT(*)` |

---

## Depois que a migration está pronta

1. `npm run build:web` — o build quebra se algum código consome a collection com tipo errado.
2. `git add pocketbase/pb_migrations/ && git commit -m "feat(pb): ..."`.
3. `git push`.
4. Ao subir o PB num ambiente novo, a migration é aplicada no boot — confira o log de startup.

## Checklist

- [ ] Arquivo novo com timestamp **maior** que o último
- [ ] Nenhuma migration já commitada foi editada
- [ ] Opções de field **diretas** no field (sem `options: {}`)
- [ ] `new Field({...})` em field novo de collection existente
- [ ] Idempotente (roda 2x sem quebrar)
- [ ] `down` com `try/catch`
- [ ] Testada com `pb_data` limpo + `sqlite3`
- [ ] `npm run build:web` verde
- [ ] Commitada no git (sem ela, o schema não existe em outro ambiente)
