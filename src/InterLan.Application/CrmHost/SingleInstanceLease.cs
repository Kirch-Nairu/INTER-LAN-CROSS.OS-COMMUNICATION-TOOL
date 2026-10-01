using System.Text;

namespace InterLan.Application.CrmHost;

public sealed class SingleInstanceLease : IAsyncDisposable, IDisposable
{
    private readonly string _lockPath;
    private FileStream? _stream;
    private bool _disposed;

    private SingleInstanceLease(string lockPath, FileStream stream)
    {
        _lockPath = lockPath;
        _stream = stream;
    }

    public string LockPath => _lockPath;

    public static SingleInstanceLease Acquire(string lockPath)
    {
        if (string.IsNullOrWhiteSpace(lockPath))
        {
            throw new ArgumentException("A lock path is required.", nameof(lockPath));
        }

        lockPath = Path.GetFullPath(lockPath);
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        FileStream stream;
        try
        {
            stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 256,
                FileOptions.WriteThrough);
        }
        catch (IOException ex)
        {
            throw new HostAlreadyRunningException(lockPath, ex);
        }

        try
        {
            stream.SetLength(0);
            var payload = Encoding.UTF8.GetBytes($"pid={Environment.ProcessId}\nacquired={DateTimeOffset.UtcNow:O}\n");
            stream.Write(payload, 0, payload.Length);
            stream.Flush(flushToDisk: true);
            stream.Position = 0;
            return new SingleInstanceLease(lockPath, stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream?.Dispose();
        _stream = null;
        TryDeleteLockFile();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void TryDeleteLockFile()
    {
        try
        {
            File.Delete(_lockPath);
        }
        catch (IOException)
        {
            // A stale pathname is recoverable because ownership is the exclusive handle, not file existence.
        }
        catch (UnauthorizedAccessException)
        {
            // Failure to remove metadata must not invalidate a lease that was already released.
        }
    }
}

public sealed class HostAlreadyRunningException : InvalidOperationException
{
    public HostAlreadyRunningException(string lockPath, Exception innerException)
        : base($"Another INTER-LAN host owns the runtime lease at '{lockPath}'.", innerException)
    {
        LockPath = lockPath;
    }

    public string LockPath { get; }
}
