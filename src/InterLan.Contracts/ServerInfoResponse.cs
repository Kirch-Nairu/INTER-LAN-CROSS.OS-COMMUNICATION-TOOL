using InterLan.Domain;

namespace InterLan.Contracts;

public sealed record ServerInfoResponse(
    string ApiVersion,
    bool Configured,
    Guid? ServerId,
    string? ServerName,
    RuntimeMode RuntimeMode,
    bool NativeDesktopSupported,
    bool WebClientSupported);
