using System.Net.Http.Json;
using RateLimiter;
using TairitsuSora.Commands.MinecraftServerManager;

namespace TairitsuSora.TairitsuSora.Commands.MinecraftServerManager;

public class HttpClientWrapper(
    HttpClient client, TimeLimiter throttler, CancellationToken token = default)
{
    public HttpClient Client => client;
    public CancellationToken Token => token;

    public async ValueTask Get(Uri uri)
    {
        (await throttler.Enqueue(
            () => client.GetAsync(uri, cancellationToken: Token), Token))
            .EnsureSuccessStatusCode();
    }

    public async ValueTask<T> GetFromJson<T>(Uri uri)
    {
        var res = await throttler.Enqueue(
            () => client.GetFromJsonAsync<ResponseData<T>>(uri, cancellationToken: Token), Token);
        return res!.Data;
    }

    public async ValueTask Post(Uri uri, HttpContent? content)
    {
        (await throttler.Enqueue(
                () => client.PostAsync(uri, content, Token), Token))
            .EnsureSuccessStatusCode();
    }

    public async ValueTask Delete(Uri uri, HttpContent? content = null)
    {
        HttpRequestMessage request = new()
        {
            Method = HttpMethod.Delete,
            RequestUri = uri
        };
        if (content is not null) request.Content = content;
        (await throttler.Enqueue(
                () => client.SendAsync(request, Token), Token))
            .EnsureSuccessStatusCode();
    }
}
