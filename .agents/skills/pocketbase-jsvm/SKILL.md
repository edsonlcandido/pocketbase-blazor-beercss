---
name: pocketbase-jsvm
description: Escrever, depurar ou modificar hooks JavaScript do PocketBase neste template (pocketbase/pb_hooks/*.pb.js). Cobre hooks de evento (onRecordAfterCreateRequest, etc.) e rotas customizadas (routerAdd("POST", "/api/...", ...)), além dos erros clássico do JSVM no PB 0.40+: $http.send devolvendo Uint8Array, $tokens removido, e.next() em AfterHooks, saveRecord dentro de AfterSuccess descartado pela transação. Use quando o pedido mencionar hook, pb_hooks, $http, $app, $tokens, $os.getenv, JSVM, routerAdd, ou qualquer JS do lado do PocketBase.
---

# PocketBase — JSVM (hooks e rotas)

O PB 0.40.5 roda JS num VM Goja. Hooks vivem em `pocketbase/pb_hooks/*.pb.js` — **extensão case-sensitive**: `*.js`, nunca `*.pb.txt` nem `*.pb.go`.

Globais disponíveis: `$http`, `$app`, `$os`, `$tokens` (removido no 0.23+), `toString`, `toBytes`, `readerToString`, `DynamicModel`, `Record`, `Collection`, `Field`, `dbx`, além dos helpers de registro (`routerAdd`, `onRecordAfterXxxRequest`…).

Este template está em **PB 0.40.5 (>= 0.23, >= 0.27)**, então:

- `resp.body` de `$http.send` é **`Uint8Array`** → use `resp.json` ou `toString(resp.body)`.
- `$app.dao()` **não existe** → chame `$app.X()` direto.
- `$tokens` foi **removido** → use `record.newAuthToken()` etc.

---

## Hooks neste repositório

```
pocketbase/pb_hooks/
└── main.pb.js            # SPA routing (web app em /app/*)
```

Conteúdo atual (não quebrar):

```js
/// <reference path="../pb_data/types.d.ts" />

routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
```

> O `true` é o **SPA fallback**: sem ele, `/app/dashboard` dá 404 em vez de servir o `index.html`.
> Se precisar de rotas de API customizadas, **crie um arquivo com prefixo numérico** (ex: `00-api.pb.js`) — o PB processa os arquivos em ordem alfabética, e o prefixo garante que a rota de API seja registrada **antes** do catch-all `/{path...}`.

---

## Workflow

1. **Confirme a versão do PB** — este projeto é 0.40.5, então os padrões abaixo valem direto.
2. **Pegue o body/contexto**:
   - rotas: `e.requestInfo().body` (Uint8Array) — prefira `e.bindBody(new DynamicModel({...}))` a `JSON.parse` manual (negocia JSON, form e multipart);
   - hooks de evento: `e.record`.
3. **Envolva o corpo do handler em `try { ... } catch (err) { ... }`.** Sem isso, a exceção vira um HTTP 500 genérico e a mensagem real se perde. Logue `err.message` **e** `err.stack` no stdout.
4. **Leia o body do `$http.send` com os helpers oficiais** (ver abaixo).
5. **CRUD de record**: `$app.findXxx` / `$app.save` / `$app.delete` direto. Env var com `$os.getenv("NAME")` (devolve `undefined` se não existir — sempre use `||` com default).
6. **Erros esperados 4xx**: `throw new BadRequestError(...)`, `NotFoundError`, `ForbiddenError`, `UnauthorizedError`. O PB serializa como JSON com o status certo. Deixe exceção solta só pra bug 5xx de verdade.

---

## `$http.send` — o padrão crítico

`resp.body` é **sempre `Uint8Array`**. Concatenar com `+` dispara `Array.prototype.toString()` e produz uma string de bytes separada por vírgula (`"123,10,32,32,..."`), que quebra qualquer `JSON.parse`, `substring` ou `String.length` depois.

```js
const resp = $http.send({ url, method: "GET", timeout: 30 });

// (a) melhor quando o upstream devolve JSON:
const data = resp.json;
if (!data || typeof data !== "object") { /* trata */ }

// (b) melhor quando precisa do texto bruto (log, parse custom):
const text = toString(resp.body);
const data = JSON.parse(text);

// (c) só bytes (download de arquivo):
const bytes = toBytes(resp.body);
```

```js
// ❌ ERRADO — corrompe a resposta silenciosamente:
JSON.parse(resp.body)            // parseia "123,10,..." e explode
resp.body.substring(0, 100)      // TypeError: Object has no member 'substring'
String(resp.body).length         // devolve nº de bytes, não de chars
"prefix: " + resp.body           // log "123,10,32,..."
```

> `$http.send` é **síncrono** — não existe `await`/`Promise` no JSVM. A chamada bloqueia a thread da requisição: sempre defina `timeout` (30s+) e não encadeie várias chamadas HTTP sequenciais quando dá pra paralelizar.

---

## Outros gotchas do JSVM (PB 0.40.5)

- **`let`/`const` no topo do arquivo NÃO são compartilhados entre handlers.** Cada callback de `routerAdd` roda num contexto novo do VM. Declare as constantes **dentro** do handler, senão ficam `undefined` em produção (e funcionam em teste isolado).
- **`$tokens` foi REMOVIDO no v0.23.** Use `record.newAuthToken()`, `record.newVerificationToken()`, `record.newFileToken()`, `record.newPasswordResetToken()`, `record.newEmailChangeToken(newEmail)`. Erro: `$tokens is not defined`.
- **`$os.getenv("X")` devolve `undefined`** (não lança) se a variável não existir. Sempre dê default com `||`.
- **`Record` é classe**: `new Record(collection)` → `record.set("k", v)` → `$app.save(record)`. Esquecer o `save` descarta a mudança silenciosamente.
- **Erros custom**: `throw new BadRequestError("msg", null)`, `NotFoundError`, `ForbiddenError`, `UnauthorizedError` (o segundo argumento é o `data` serializado no JSON).
- **`console.log` vai pro stdout**, não pro cliente. Bom pra debug, mas não mande dado sensível (vai pro log do container).
- **Não chame `e.next()` em `onRecordAfter*Success` / `onRecordAfter*Error`.** Só os hooks `*Execute` wrappam a operação e precisam de `e.next()` para continuar. Nos After, é no-op ou erro silencioso.
  - **Sintoma:** o hook "executa" (o efeito aparece no record retornado) mas o `console.log` que vem **depois** do `e.next()` nunca aparece.
  - **Correção:** remova o `e.next()` dos AfterHooks.
- **`$app.dao().saveRecord()` dentro de `onRecordAfterCreateSuccess` é descartado pela transação.** O INSERT inicial persiste; qualquer modificação via `dao().saveRecord` no hook é revertida no commit.
  - **Correção:** use `onRecordUpdateExecute` quando precisar alterar o record criado, ou faça um `POST`/`PATCH` separado depois do INSERT. Para logs e efeitos colaterais, AfterSuccess funciona normal.
- **Query com filtro**: use `$app.findRecordsByFilter()`. A forma antiga `{ filter: ... }` foi removida no 0.23+, e `$app.filter(...)` **não existe**.
  ```js
  // ✅ CERTO:
  $app.findRecordsByFilter("posts",
    "author = {:aid} && published = true",
    "",      // sort (vazio = ordem padrão)
    500,     // limit
    0,       // offset
    { aid: authorId },   // dbx.Params
  )
  // ❌ ERRADO (sintaxe removida):
  $app.findAllRecords("posts", { filter: "..." })
  // ❌ ERRADO (não existe):
  $app.filter("author = {:aid}", { aid: authorId })
  ```
  - **Sintoma:** `TypeError: Object has no member 'filter'` no stdout, ou query que retorna vazio sem erro.

---

## Migração da API `Dao` do v0.22 (não escrever código antigo)

No v0.23 a abstração `Dao` foi **removida** — os métodos subiram um nível, direto pro `$app`. Código do v0.22 só quebra quando a linha é executada (sem aviso no startup).

| Operação | v0.22 (antigo, não usar) | v0.23+ (use este) |
|---|---|---|
| Achar collection | `$app.dao().findCollectionByNameOrId("x")` | `$app.findCollectionByNameOrId("x")` |
| Achar record por id | `$app.dao().findRecordById("x", id)` | `$app.findRecordById("x", id)` |
| Achar primeiro por campo | `$app.dao().findFirstRecordByData("x", "k", v)` | `$app.findFirstRecordByData("x", "k", v)` |
| Achar primeiro por filtro | `$app.dao().findFirstRecordByFilter(col, ...)` | `$app.findFirstRecordByFilter("x", ...)` |
| Salvar | `$app.dao().saveRecord(rec)` | `$app.save(rec)` |
| Salvar sem validação | — | `$app.saveNoValidate(rec)` |
| Deletar | `$app.dao().deleteRecord(rec)` | `$app.delete(rec)` |
| Auth por email | `$app.dao().findAuthRecordByEmail("users", e)` | `$app.findAuthRecordByEmail("users", e)` |
| Gerar token de auth | `$tokens.recordAuthToken($app, rec)` | `record.newAuthToken()` |

---

## Bug conhecido no PB 0.40.5 — `e.string`/`e.json` perdem o body depois de `$http.send`

**Sintoma:** um handler chama `$http.send(...)`, recebe resposta válida (headers, `resp.json`, `resp.body` todos preenchidos — o `console.log` prova), e devolve via `e.json(status, data)` ou `e.string(status, str)`. O **status HTTP está correto**, mas vem `Content-Length: 0` e body vazio no browser/curl. Sem exceção, sem erro no log.

**Reprodução:**

```js
// Rota A: sem $http.send — funciona
routerAdd("GET", "/api-teste", (e) => e.string(200, "ok"));
// → curl: 200, body "ok"  ✓

// Rota B: com $http.send — body some
routerAdd("GET", "/api-teste2", (e) => {
  const resp = $http.send({ url: "...", method: "GET", timeout: 30 });
  return e.string(resp.status, toString(resp.body));
});
// → curl: 200, body ""  ✗
```

**Workarounds tentados, nenhum funciona:** `resp = null` antes do `e.string`; `e.json` em vez de `e.string`; `e.responseHeader().set(...)` (lança panic — método não existe neste JSVM); `e.string(status, body, { "Content-Type": ... })` (3ª arg ignorada).

**Workaround que funciona (usado em produção):** expor o upstream como rota **do próprio PB** que grava o resultado num record, e deixar o frontend ler o record (realtime/poll). Alternativa: o frontend chama o upstream direto (cross-origin), com CORS no upstream.

**Ao atualizar o PB**, re-teste esse cenário antes de religar a rota com `$http.send`.

---

## Depurando um hook quebrado

1. **Confirme que o hook está no lugar certo** — `pocketbase/pb_hooks/<nome>.pb.js` (case-sensitive). No container: `ls -la /app/pb_hooks/`.
2. **Filtre o log pelo seu marcador.** Prefira prefixar cada `console.log` com `[nome-do-hook]`:
   ```bash
   docker logs -f <container> 2>&1 | grep '\[nome-do-hook\]'
   ```
3. **Reproduza no cliente** — DevTools → Network mostra o status; o `errorBody()` da resposta costuma trazer a mensagem do `BadRequestError`/`UnauthorizedError` verbatim.
4. **Log granular por etapa** (request recebido → header lido → body parseado → chamada ao upstream → status → body → JSON parse → resposta final). O log que falta diz exatamente onde explodiu.
5. **Erros comuns e a causa:**

| Erro | Causa |
|---|---|
| `Object has no member 'dao'` | código pré-v0.23, ou `dao().saveRecord()` dentro de AfterSuccess |
| `Object has no member 'filter'` | `$app.filter(...)` (não existe) → use `findRecordsByFilter` |
| `TypeError: Object has no member 'substring'` | body do `$http.send` concatenado como string |
| `$tokens is not defined` | API antiga → `record.newAuthToken()` |
| hook "executa" mas o log depois nunca aparece | `e.next()` dentro de AfterSuccess → remova |
| `saveRecord` no AfterSuccess não persiste | transação descarta o save → use `onRecordUpdateExecute` |
| resposta 200 com body vazio | bug do `e.string`/`e.json` pós-`$http.send` (ver acima) |

---

## Migrations

Migrations deste template ficam em `pocketbase/pb_migrations/*.js`.
Guia completo (sintaxe de field, idempotência, teste local com `pb_data` + `sqlite3`, tabela de erros) em
**`.agents/skills/pocketbase-migrations/SKILL.md`** — leia antes de mexer em `pb_migrations/`.

Duas regras que já custaram rollback:

1. **Nunca editar uma migration depois de commitada.** O PB registra as aplicadas em `pb_data/_migrations` e não reaplica. Se ficou estado parcial, escreva uma **nova** migration, idempotente, com timestamp maior.
2. **Timestamp do arquivo = Unix epoch (UTC)**, e tem que ser **maior** que o maior existente em `pb_migrations/`.
