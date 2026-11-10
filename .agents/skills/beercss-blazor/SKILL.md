---
name: beercss-blazor
description: Usar o BeerCSS 5 (Material Design 3, CSS puro) dentro do Blazor WebAssembly deste template — classes de grid, campo, botão, card, chip, badge, switch, ícones Material Symbols, tema flat e o que NÃO existe (build step, .card, Tailwind). Use quando o pedido mencionar "BeerCSS", "beer.css", "estilo", "CSS", "layout", "grid", "botão", "formulário", "ícone", "tema", "cor", "dark mode" ou qualquer mudança em apps/*/wwwroot/css.
---

# BeerCSS no Blazor

O BeerCSS v5 é **CSS puro** (Material Design 3). Sem build step, sem Tailwind, sem JS obrigatório — só classes no HTML.

## O que está vendorizado

```
apps/<app>/wwwroot/beercss/
├── beer.min.css                      # 88 KB — CSS completo
└── material-symbols-outlined.woff2   # 464 KB — fonte dos ícones
```

Carregado no `index.html`:

```html
<link rel="stylesheet" href="beercss/beer.min.css" />
<link rel="stylesheet" href="css/app.css" />
```

`css/app.css` vem **depois** → o app sobrescreve o BeerCSS quando precisa.

> ⚠️ **O `beer.min.js` NÃO está vendorizado.** Ele só é necessário para componentes interativos (tabs, menus, dialogs) e **não** pode ser carregado junto do `beer.min.css` (o JS injeta os módulos de CSS de novo → duplicado).

---

## Mapa rápido das classes

### Grid (12 colunas)

```html
<div class="grid">
    <div class="s12 m6 l4">...</div>   <!-- small: 12, medium: 6, large: 4 -->
</div>
```

`grid` já aplica o gap. `grid no-space` tira o gap.

### Botão

```html
<button class="button">Primário</button>
<button class="button border">Contornado</button>
<button class="button large">Grande</button>
<button class="button small">Pequeno</button>
<button class="button error">Destrutivo</button>
<button class="button" disabled>Desabilitado</button>

<a class="button" href="/app/">Link-botão</a>
```

### Campo (input, textarea, select)

```html
<!-- com label flutuante -->
<div class="field label border">
    <input type="email" placeholder=" " />
    <label>E-mail</label>
</div>

<!-- com ícone à esquerda -->
<div class="field border prefix">
    <i>search</i>
    <input type="search" placeholder=" " />
    <label>Buscar</label>
</div>

<!-- erro -->
<div class="field label border invalid">
    <input type="email" placeholder=" " />
    <label>E-mail</label>
</div>
```

> ⚠️ O `placeholder=" "` é **obrigatório** no campo com label flutuante — o CSS usa `:placeholder-shown` pra decidir quando o label sobe.

### Card

**No BeerCSS v5, card é a tag `<article>`** — não existe `.card`.

```html
<article class="border">      <!-- article = card; .border = versão sem sombra -->
    <div>conteúdo</div>
</article>
```

O template define `.card`, `.card-header` e `.card-body` **no `css/app.css` do app** — são helpers do projeto, não do BeerCSS.

### App-shell (`:has(>main)`) — a armadilha que derruba o layout

O BeerCSS transforma **qualquer elemento que tenha um `<main>` direto** num grid de 3 colunas:

```css
:has(>main) {
  display: grid;
  grid-template-columns: auto minmax(0,1fr) auto;
  grid-template-rows: auto auto minmax(0,1fr) auto auto;
  grid-template-areas: "left top right" "left header right"
                       "left main right" "left footer right" "left bottom right";
  min-block-size: 100dvh;
}
nav.top  { grid-area: top }
nav.left { grid-area: left }
main     { grid-area: main; padding: .5rem; overflow: clip }
footer   { grid-area: footer }
```

Ou seja: **o `<nav>` sem a classe de área (`top`/`left`/`right`/`bottom`) cai por auto-placement na coluna `left`** (que tem a largura do conteúdo) → a barra fica estreita, cortada, e o `<main>` é empurrado para o lado.

O idioma correto (usado nos dois apps deste template):

```html
<div class="app-shell">
    <nav class="top">...</nav>
    <main>...</main>
</div>
```

```css
.app-shell > nav.top {
    grid-column: 1 / -1;   /* ocupa todas as colunas */
    justify-content: space-between;
}
```

> `<nav>` **precisa** ter uma das classes de área. E o `<main>` não pode ser filho direto do `body`/raiz sem wrapper — cada `<main>` cria o grid no pai.

### Lista

```html
<ul class="list border no-space">
    <li>Item 1</li>
    <li>Item 2</li>
</ul>
```

### Chip / Badge

```html
<span class="chip">Rascunho</span>
<span class="chip fill">Publicado</span>
<span class="badge">12</span>
```

### Switch / Checkbox / Radio

```html
<label class="switch">
    <input type="checkbox" checked />
    <span></span>
</label>
```

### Ícones (Material Symbols)

O nome do ícone é o **texto** dentro da tag `<i>`:

```html
<i>rocket_launch</i>
<i>logout</i>
<i>person</i>
<i class="tiny">favorite</i>
<i class="large">settings</i>
```

Tamanhos: `tiny` (1rem), `small` (1.25rem), `medium` (1.5rem, padrão), `large` (1.75rem), `extra` (2rem).

### Progress

```html
<progress class="circle" />              <!-- spinner indeterminado -->
<progress class="circle small" />
<progress value="60" max="100" />        <!-- barra -->
```

---

## Armadilhas do CSS do BeerCSS (leia antes de mexer no layout)

Extraídas do `beer.min.css` — todas quebram layout silenciosamente.

| Regra do BeerCSS | Efeito | Como escapar |
|---|---|---|
| `button { box-sizing: content-box }` (via `.button,button`) | `<button class="button large-width">` fica `100% + padding + borda` e **estoura o container**; `<a class="button">` (border-box via `*`) fica certo — mesma classe, larguras diferentes | `button { box-sizing: border-box; }` no `app.css` |
| `.button,button { block-size: var(--_size); padding: 0 var(--_padding) }` | todo `<button>` ganha altura fixa de 2.5rem, independente do conteúdo | `block-size: auto` na classe do app |
| `.primary { background-color: var(--primary) !important; color: var(--on-primary) !important }` | a classe utilitária `.primary` **pinta o elemento inteiro** — e não dá para sobrescrever sem `!important` | prefixe as classes do projeto (`tone-primary`, `tone-accent`) |
| `.secondary { … !important }` | idem para `.secondary` | idem |
| `nav:is(.left,.right,.top,.bottom) a:not(.button,.chip) { display:flex; flex-direction:column; inline-size:3.5rem; font-size:.8rem }` | todo `<a>` do `nav.top` vira **coluna de 3.5rem**: brand empilhado, chip de usuário quebrando o texto e estourando a barra | sobrescreva com seletor de especificidade maior: `.app-shell > nav.top a.brand { … }` |
| `.wrap { display: block; white-space: normal }` | `.wrap` **não** é flex-wrap | use `flex-wrap: wrap` no CSS do app |
| `:not(.grid,nav,.row)>:is(…,h1,…,p,…) { margin-block-start: 1rem }` | headings e parágrafos irmãos ganham margem de topo automática | `margin: 0` no container flex |

> Especificidade: o seletor do BeerCSS `nav:is(.left,.right,.top,.bottom) a:not(.button,.chip)` vale (0,2,2). Um seletor do app como `.app-shell > nav.top a.brand` vale (0,3,2) e **vence** — sem precisar de `!important`.

---

## Helpers

| Classe | Efeito |
|---|---|
| `no-margin` / `small-margin` / `large-margin` | margem |
| `no-padding` / `small-padding` / `large-padding` | padding |
| `left-margin` / `right-margin` / `top-margin` / `bottom-margin` | lado específico |
| `left-align` / `right-align` / `center-align` | alinhamento |
| `middle-align` / `top-align` / `bottom-align` | alinhamento vertical (flex) |
| `row` | linha flex |
| `primary-text` / `secondary-text` / `error-text` | cor do texto |
| `primary` / `secondary` / `error` | cor de fundo |
| `truncate` | texto com reticências |
| `no-wrap` / `wrap` | quebra de linha |
| `m l` | visível a partir de medium (esconde no small) |

---

## Tema flat (regra do projeto)

**No máximo 1 primária + 1 accent, ambas sólidas. Sem gradiente.**

Os tokens Material 3 são sobrescritos em `apps/<app>/wwwroot/css/app.css`:

```css
:root,
body.light {
    --primary: #0f766e;            /* teal — primária */
    --on-primary: #ffffff;
    --primary-container: #ccfbf1;
    --on-primary-container: #00201d;

    --secondary: #1d4ed8;          /* azul — accent */
    --on-secondary: #ffffff;
    --secondary-container: #dbe4ff;
    --on-secondary-container: #00174a;

    --tertiary: #1d4ed8;           /* tertiary = accent (só 2 cores) */
    /* ... */
}

@media (prefers-color-scheme: dark) {
    :root {
        --primary: #5eead4;
        /* ... */
    }
}
```

> **A paleta é IDÊNTICA nos dois apps** (`apps/landing` e `apps/web`): teal na primária, azul no accent. Antes as duas apps trocavam os papéis e o site mudava de cor ao clicar em "Acessar App". Se for mudar a paleta, mude nos **dois** arquivos e confira que os blocos de token ficaram byte-a-byte iguais.

> O bloco `:root, body.light` (em vez de só `:root`) é necessário porque `body.light` tem **especificidade maior** — se ficasse só em `:root`, o valor original do BeerCSS venceria.

### Onde mexer no estilo

| Tipo | Onde |
|---|---|
| Token de cor (tema) | `wwwroot/css/app.css` — bloco `:root` |
| Layout do app | `wwwroot/css/app.css` — classes utilitárias do projeto |
| Ajuste pontual | classe no `class="..."` do elemento |

**Nunca** use gradiente (`linear-gradient`, `radial-gradient`) — nem no CSS do app.

---

## Anti-padrões

| ❌ Não faça | ✅ Faça |
|---|---|
| `class="card"` esperando o estilo do BeerCSS | `<article>` + as classes do app |
| `<i class="material-symbols-outlined">` | `<i>` puro (a fonte já é a padrão) |
| `<input>` sem `placeholder=" "` em campo com label | sempre `placeholder=" "` |
| `<style>` dentro do `.razor` | `wwwroot/css/app.css` |
| `bg-gradient-to-r` (Tailwind) | cor sólida |
| Carregar `beer.min.js` junto do `beer.min.css` | só o CSS |
| Classe Tailwind (`flex`, `text-2xl`) | equivalente BeerCSS (`row`, `h5`) |
| `<nav>` sem `top`/`left`/`right`/`bottom` dentro de um app-shell | sempre com a classe de área + `grid-column: 1 / -1` |
| Classe utilitária `.primary`/`.secondary` para dar "tom" a um card | classe do projeto com prefixo (`tone-primary`) |
| Confiar que `.wrap` faz flex-wrap | `flex-wrap: wrap` no CSS do app |

---

## Checklist

- [ ] Classes do BeerCSS (não Tailwind) no markup
- [ ] `<i>` com o nome do ícone como texto
- [ ] `placeholder=" "` em todo campo com label flutuante
- [ ] Card como `<article>`
- [ ] Nenhum gradiente no CSS
- [ ] Tema flat: 1 primária + 1 accent, **idêntico nos dois apps**
- [ ] `<nav>` com classe de área (`top`) dentro do app-shell
- [ ] `button { box-sizing: border-box }` no `app.css` dos dois apps
- [ ] Nenhuma classe utilitária de cor (`.primary`, `.secondary`) em elemento do app

## Skills relacionadas

- `.agents/skills/blazor-page/SKILL.md` — página Razor com form e componentes
- `AGENTS.md` (raiz) — tema e regras do projeto
