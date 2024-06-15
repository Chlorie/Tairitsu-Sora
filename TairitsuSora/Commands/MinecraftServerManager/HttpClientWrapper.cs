using System.Net.Http.Json;
using RateLimiter;
using TairitsuSora.Commands.MinecraftServerManager;

namespace TairitsuSora.TairitsuSora.Commands.MinecraftServerManager;

public class HttpClientWrapper(
    HttpClient client, TimeLimiter throttler, CancellationToken token = default)
{
    public HttpClient Client => client;
    public CancellationToken Token => token;

    public async ValueTask Get(Uri uri) =>
        await CheckStatusAndWrap(await throttler.Enqueue(() => client.GetAsync(uri, Token), Token));

    public async ValueTask<T> GetFromJson<T>(Uri uri)
    {
        var responseMessage = await throttler.Enqueue(() => client.GetAsync(uri, Token), Token);
        await CheckStatusAndWrap(responseMessage);
        return (await responseMessage.Content.ReadFromJsonAsync<ResponseData<T>>(token))!.Data;
    }

    public async ValueTask Post(Uri uri, HttpContent? content) =>
        await CheckStatusAndWrap(await throttler.Enqueue(() => client.PostAsync(uri, content, Token), Token));

    public async ValueTask Delete(Uri uri, HttpContent? content = null)
    {
        HttpRequestMessage request = new()
        {
            Method = HttpMethod.Delete,
            RequestUri = uri
        };
        if (content is not null) request.Content = content;
        await CheckStatusAndWrap(await throttler.Enqueue(() => client.SendAsync(request, Token), Token));
    }

    private async ValueTask CheckStatusAndWrap(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string? message = null;
        try { message = (await response.Content.ReadFromJsonAsync<ResponseData<string>>(token))?.Data; }
        catch (Exception) { response.EnsureSuccessStatusCode(); }
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
