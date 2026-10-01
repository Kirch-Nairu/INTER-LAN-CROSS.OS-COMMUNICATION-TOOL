using System.Diagnostics;

namespace InterLan.Application.CrmHost;

public sealed record ProcessLaunchSpec(
    string Component,
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? Environment = null,
    bool CaptureOutput = true)
{
    public ProcessStartInfo ToStartInfo()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Component);
        ArgumentException.ThrowIfNullOrWhiteSpace(FileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = CaptureOutput,
            RedirectStandardError = CaptureOutput
        };

        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            startInfo.WorkingDirectory = Path.GetFullPath(WorkingDirectory);
        }

        foreach (var argument in Arguments)
        {
            if (argument.IndexOf('\0') >= 0)
            {
                throw new InvalidOperationException("Process arguments cannot contain NUL characters.");
            }

            startInfo.ArgumentList.Add(argument);
        }

        if (Environment is not null)
        {
            foreach (var pair in Environment)
            {
                if (pair.Value is null)
                {
                    startInfo.Environment.Remove(pair.Key);
                }
                else
                {
                    startInfo.Environment[pair.Key] = pair.Value;
                }
            }
        }

        return startInfo;
    }
}
