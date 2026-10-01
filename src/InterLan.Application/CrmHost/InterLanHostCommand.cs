namespace InterLan.Application.CrmHost;

public enum InterLanHostCommandKind
{
    Start,
    Stop,
    Status
}

public sealed record InterLanHostCommand(
    InterLanHostCommandKind Kind,
    bool RemoteAccess,
    bool JsonOutput)
{
    public static InterLanHostCommand Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new ArgumentException("Expected one of: start, stop, status.", nameof(args));
        }

        var verb = args[0].Trim().ToLowerInvariant();
        var options = args.Skip(1).ToArray();

        return verb switch
        {
            "start" => ParseStart(options),
            "stop" => ParseStop(options),
            "status" => ParseStatus(options),
            _ => throw new ArgumentException($"Unknown INTER-LAN command '{args[0]}'.", nameof(args))
        };
    }

    private static InterLanHostCommand ParseStart(IReadOnlyList<string> options)
    {
        var remote = false;
        foreach (var option in options)
        {
            switch (option)
            {
                case "--remote":
                    remote = true;
                    break;
                case "--local-only":
                    remote = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown start option '{option}'.", nameof(options));
            }
        }

        return new InterLanHostCommand(InterLanHostCommandKind.Start, remote, false);
    }

    private static InterLanHostCommand ParseStop(IReadOnlyList<string> options)
    {
        if (options.Count != 0)
        {
            throw new ArgumentException("The stop command accepts no options.", nameof(options));
        }

        return new InterLanHostCommand(InterLanHostCommandKind.Stop, false, false);
    }

    private static InterLanHostCommand ParseStatus(IReadOnlyList<string> options)
    {
        if (options.Count == 0)
        {
            return new InterLanHostCommand(InterLanHostCommandKind.Status, false, false);
        }

        if (options.Count == 1 && string.Equals(options[0], "--json", StringComparison.Ordinal))
        {
            return new InterLanHostCommand(InterLanHostCommandKind.Status, false, true);
        }

        throw new ArgumentException("The status command accepts only --json.", nameof(options));
    }
}
