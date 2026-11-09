/*
 * Helpers JS usados pelo app Blazor.
 * Mantidos em um arquivo único e sem dependência externa.
 */
window.pocketbase = {
    storage: {
        get: function (key) {
            try {
                return window.localStorage.getItem(key);
            } catch (error) {
                return null;
            }
        },
        set: function (key, value) {
            try {
                window.localStorage.setItem(key, value);
            } catch (error) {
                /* modo privado / quota cheia — a sessão só vive em memória */
            }
        },
        remove: function (key) {
            try {
                window.localStorage.removeItem(key);
            } catch (error) {
                /* ignora */
            }
        }
    },

    /* Lê um IBrowserFile e devolve uma data URL para pré-visualização. */
    readAsDataUrl: async function (streamRef) {
        const buffer = await streamRef.arrayBuffer();
        const blob = new Blob([buffer]);
        return await new Promise(function (resolve, reject) {
            const reader = new FileReader();
            reader.onload = function () { resolve(reader.result); };
            reader.onerror = function () { reject(reader.error); };
            reader.readAsDataURL(blob);
        });
    }
};
