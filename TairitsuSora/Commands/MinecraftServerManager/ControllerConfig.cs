namespace TairitsuSora.Commands.MinecraftServerManager;

// ReSharper disable ClassNeverInstantiated.Global

public record ControllerConfig(
    long AdminId,
    string ApiEndpoint,
    string ApiKey,
    string Uuid,
    string RemoteUuid,
    bool EnableMessageForwarding = false,
    BackupConfig? BackupConfig = null
);

public record BackupConfig(
    long MaxTotalSize = 0,
    string WorldDirectory = "",
    string BackupDirectory = "",
    bool EnableDailyBackup = false
);
