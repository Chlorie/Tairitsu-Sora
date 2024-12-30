using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using YukariToolBox.LightLog;

namespace TairitsuSora.Commands.CelestePacePing;

[ApiController]
[Route("/pacePing/{group:long}-{user:long}/{token}")]
public class FakeDiscordWebhookBase(IDiscordRequestProcessor processor) : ControllerBase
{
    [HttpPost]
    public async ValueTask<IActionResult> PostMessage(
        long group, long user, string token, [FromQuery] bool wait, [FromBody] DiscordWebhookRequest request)
    {
        if (!wait)
            Log.Warning("PacePing", "Unknown request with wait=false");
        var id = Guid.NewGuid();
        _cache.Set(id, request, new MemoryCacheEntryOptions { Size = 1 });
        await processor.ProcessRequest(new MemberId(group,user), token, request);
        return Ok();
    }

    [HttpPatch("messages/{id}")]
    public async ValueTask<IActionResult> PatchMessage(string id,
        long group, long user, string token, [FromQuery] bool wait, [FromBody] DiscordWebhookRequest request)
    {
        if (!wait)
            Log.Warning("PacePing", "Unknown request with wait=false");
        var guid = Guid.Parse(id);
        _cache.Set(guid, request, new MemoryCacheEntryOptions { Size = 1 });
        await processor.ProcessRequest(new MemberId(group, user), token, request);
        return Ok();
    }

    private MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1024 });
}

public record struct MemberId(long GroupId, long UserId);

public interface IDiscordRequestProcessor
{
    ValueTask<IActionResult> ProcessRequest(MemberId member, string token, DiscordWebhookRequest request);
}
