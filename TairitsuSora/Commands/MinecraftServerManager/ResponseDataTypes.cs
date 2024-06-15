using System.Text.Json.Serialization;

namespace TairitsuSora.Commands.MinecraftServerManager;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable NotAccessedPositionalProperty.Global

public record ResponseData<T>(
    [property: JsonPropertyName("status")] int StatusCode,
    [property: JsonPropertyName("data")] T Data,
    [property: JsonPropertyName("time")] long Time
);

public enum PingStatusCode { Stopped, Stopping, Starting, Running, Unknown }

public record PingData(
    [property: JsonPropertyName("status")] PingStatusCode Status = PingStatusCode.Unknown,
    [property: JsonPropertyName("info")] PingData.InfoData? Info = null,
    [property: JsonPropertyName("processInfo")] PingData.ProcessInfoData? ProcessInfo = null
)
{
    public record InfoData(
        [property: JsonPropertyName("currentPlayers")]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        int OnlinePlayerCount,
        [property: JsonPropertyName("version")] string Version
    );

    public record ProcessInfoData(
        [property: JsonPropertyName("cpu")] float CpuUsage,
        [property: JsonPropertyName("memory")] long MemoryUsage
    );
}

public record FileStatusData(
    [property: JsonPropertyName("globalFileTask")] int GlobalFileTask,
    [property: JsonPropertyName("instanceFileTask")] int InstanceFileTask
);

public record ListFileData(
    [property: JsonPropertyName("items")] FileItemData[] Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page
);

public enum FileItemType { Directory, File, Unknown }

public record FileItemData(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("type")] FileItemType Type
);
