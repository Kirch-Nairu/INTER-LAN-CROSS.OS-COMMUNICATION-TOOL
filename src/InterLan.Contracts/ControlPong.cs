namespace InterLan.Contracts;

public sealed record ControlPong(
    string ApiVersion,
    DateTimeOffset ServerUtc,
    string Status);
