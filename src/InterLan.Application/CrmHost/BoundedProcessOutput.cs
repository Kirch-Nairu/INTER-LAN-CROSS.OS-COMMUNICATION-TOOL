namespace InterLan.Application.CrmHost;

public sealed class BoundedProcessOutput
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private readonly int _maxLines;
    private readonly int _maxLineLength;

    public BoundedProcessOutput(int maxLines = 200, int maxLineLength = 4096)
    {
        if (maxLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLines));
        }

        if (maxLineLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLineLength));
        }

        _maxLines = maxLines;
        _maxLineLength = maxLineLength;
    }

    public void Append(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        var normalized = line.Length > _maxLineLength
            ? line[.._maxLineLength]
            : line;

        lock (_gate)
        {
            _lines.Enqueue(normalized);
            while (_lines.Count > _maxLines)
            {
                _lines.Dequeue();
            }
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return _lines.ToArray();
        }
    }
}
