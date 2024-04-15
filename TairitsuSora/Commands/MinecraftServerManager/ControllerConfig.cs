namespace TairitsuSora.Commands.MinecraftServerManager;

// ReSharper disable once ClassNeverInstantiated.Global
public record ControllerConfig(
    long AdminId,
    string ApiEndpoint,
    string ApiKey,
    string Uuid,
    string RemoteUuid,
    bool EnableMessageForwarding = false
);
