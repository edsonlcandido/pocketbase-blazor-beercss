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
    --primary: #1d4ed8;            /* azul — primária */
    --on-primary: #ffffff;
    --primary-container: #dbe4ff;
    --on-primary-container: #00174a;

    --secondary: #0f766e;          /* teal — accent */
    --on-secondary: #ffffff;
    --secondary-container: #ccfbf1;
    --on-secondary-container: #00201d;

    --tertiary: #0f766e;           /* tertiary = accent (só 2 cores) */
    /* ... */
}

@media (prefers-color-scheme: dark) {
    :root {
        --primary: #adc6ff;
        /* ... */
    }
}
```

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

---

## Checklist

- [ ] Classes do BeerCSS (não Tailwind) no markup
- [ ] `<i>` com o nome do ícone como texto
- [ ] `placeholder=" "` em todo campo com label flutuante
- [ ] Card como `<article>`
- [ ] Nenhum gradiente no CSS
- [ ] Tema flat: 1 primária + 1 accent

## Skills relacionadas

- `.agents/skills/blazor-page/SKILL.md` — página Razor com form e componentes
- `AGENTS.md` (raiz) — tema e regras do projeto
