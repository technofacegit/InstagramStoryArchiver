using InstagramStoryArchiver.Application.Abstractions;
using InstagramStoryArchiver.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstagramStoryArchiver.Infrastructure.Locking;

public sealed class FileInstanceLock : IInstanceLock
{
    private readonly string _lockFilePath;
    private readonly ILogger<FileInstanceLock> _logger;
    private FileStream? _stream;
    private bool _acquired;

    public FileInstanceLock(IOptions<StorageOptions> options, ILogger<FileInstanceLock> logger)
    {
        _lockFilePath = Path.GetFullPath(options.Value.LockFilePath);
        _logger = logger;
    }

    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(_lockFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            _stream = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            var payload = System.Text.Encoding.UTF8.GetBytes($"{Environment.ProcessId}|{DateTimeOffset.UtcNow:O}");
            _stream.SetLength(0);
            _stream.Write(payload);
            _stream.Flush();
            _acquired = true;
            _logger.LogInformation("Acquired instance lock at {LockFile}", _lockFilePath);
            return Task.FromResult(true);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Another worker instance appears to be running. Lock file: {LockFile}", _lockFilePath);
            return Task.FromResult(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        if (_acquired)
        {
            try
            {
                if (File.Exists(_lockFilePath))
                {
                    File.Delete(_lockFilePath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to delete lock file {LockFile}", _lockFilePath);
            }
        }
    }
}
