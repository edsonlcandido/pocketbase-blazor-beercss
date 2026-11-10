/// <reference path="../pb_data/types.d.ts" />

/**
 * PocketBase Hook: SPA Routing + Cache explícito
 *
 * 1) Web App (SPA Blazor) em /app/* — todas as rotas do SPA são servidas
 *    com o index.html correto, para o roteador do Blazor gerenciar o
 *    lado do cliente ($apis.static com indexFallback=true).
 *
 * 2) Cache-Control explícito (middleware global routerUse).
 *    O PocketBase NÃO manda Cache-Control nos arquivos estáticos, e aí o
 *    navegador aplica cache HEURÍSTICO (10% da idade do Last-Modified):
 *    depois de um deploy ele continua servindo o CSS/HTML antigo por até
 *    horas — foi exatamente o que escondeu uma correção de layout.
 *    Aqui a regra fica explícita:
 *      - HTML/CSS/JS  → no-cache (revalida sempre; 304 é barato)
 *      - _framework/* → immutable (o build do .NET já põe hash no nome)
 *    A API fica como o PocketBase define.
 *
 * Cuidado com o "immutable": dentro de _framework só é imutável o arquivo
 * que tem FINGERPRINT no nome (ex.: "Web.2i9vp585ak.wasm"). O manifesto de
 * recursos vive em "dotnet.js", que NÃO tem hash e muda a cada build —
 * marcar o diretório inteiro como immutable faria o navegador ficar
 * preso no build antigo para sempre.
 *
 * Notas da API (PocketBase 0.40):
 *  - NÃO existe `onServe` no JSVM; o hook global de requisição é o
 *    middleware `routerUse`, que precisa chamar `e.next()` para seguir.
 *  - `e.response.header` é uma FUNÇÃO: o header se escreve em
 *    `e.response.header().set(chave, valor)`.
 *  - Funções auxiliares declaradas no topo do arquivo não são visíveis
 *    dentro do callback do middleware (escopo do goja) — a lógica fica
 *    inline por isso.
 */

routerUse((e) => {
    try {
        const path = (e.request && e.request.url) ? e.request.url.path : "";

        // Não mexe na API (JSON, arquivos, realtime).
        if (path.indexOf("/api/") !== 0) {
            // Fingerprint do .NET: 10 caracteres alfanuméricos no nome do
            // arquivo (ex.: "Web.2i9vp585ak.wasm"). Só esses são imutáveis.
            const fileName = path.substring(path.lastIndexOf("/") + 1);
            const isFingerprinted = /\.[a-z0-9]{10}\./i.test(fileName);

            e.response.header().set(
                "Cache-Control",
                isFingerprinted
                    ? "public, max-age=31536000, immutable"
                    : "no-cache"
            );
        }
    } catch (err) {
        // Nunca derruba a resposta por causa de um header.
        console.log("não foi possível setar Cache-Control:", err);
    }

    e.next();
});

// Web App: /app/*
routerAdd("GET", "/app/{path...}", $apis.static("pb_public/app", true))
