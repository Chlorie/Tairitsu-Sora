using Microsoft.AspNetCore.Mvc;
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
        if (!wait) Log.Warning("PacePing", "Unknown request with wait=false");
        return await processor.ProcessRequest(new MemberId(group,user), token, null, request);
    }

    [HttpPatch("messages/{id}")]
    public async ValueTask<IActionResult> PatchMessage(string id,
        long group, long user, string token, [FromQuery] bool wait, [FromBody] DiscordWebhookRequest request)
    {
        if (!wait) Log.Warning("PacePing", "Unknown request with wait=false");
        return await processor.ProcessRequest(new MemberId(group, user), token, id, request);
    }
}

public record struct MemberId(long GroupId, long UserId);

public interface IDiscordRequestProcessor
{
    ValueTask<IActionResult> ProcessRequest(MemberId member, string token, string? patchedId, DiscordWebhookRequest request);
}
