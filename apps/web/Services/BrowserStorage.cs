using Microsoft.JSInterop;

namespace Web.Services;

/// <summary>Acesso ao localStorage do navegador.</summary>
/// <remarks>
/// Usa a API nativa do browser via <see cref="IJSRuntime"/> — o template
/// original fazia isso através do SDK JS do PocketBase (<c>pocketbase.storage</c>),
/// que não existe aqui porque o cliente é 100% C#.
/// </remarks>
public sealed class BrowserStorage
{
    private readonly IJSRuntime _js;

    public BrowserStorage(IJSRuntime js) => _js = js;

    public ValueTask<string?> GetAsync(string key) => _js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) => _js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask RemoveAsync(string key) => _js.InvokeVoidAsync("localStorage.removeItem", key);
}
