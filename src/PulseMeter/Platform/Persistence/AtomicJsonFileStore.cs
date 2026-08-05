using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace PulseMeter.Platform.Persistence;

internal enum AtomicJsonLoadStatus
{
    Loaded,
    Missing,
    Invalid,
    Unavailable
}

internal readonly record struct AtomicJsonLoadResult<T>(AtomicJsonLoadStatus Status, T? Value = default);

internal static class AtomicJsonFileStore
{
    private const int MutexWaitMilliseconds = 100;
    private const int MutexWaitAttempts = 20;
    private const int ReadAttempts = 3;
    private static readonly TimeSpan StaleTemporaryFileAge = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static T? Load<T>(string filePath, JsonSerializerOptions options, Func<T, bool>? isValid = null)
    {
        var result = LoadWithStatus(filePath, options, isValid);
        return result.Status == AtomicJsonLoadStatus.Loaded ? result.Value : default;
    }

    internal static AtomicJsonLoadResult<T> LoadWithStatus<T>(
        string filePath,
        JsonSerializerOptions options,
        Func<T, bool>? isValid = null)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        lock (GetPathLock(normalizedPath))
        {
            using var mutex = TryCreateAcquiredMutex(normalizedPath);
            if (mutex is null)
            {
                return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Unavailable);
            }

            try
            {
                ScavengeStaleTemporaryFiles(normalizedPath);
                var primary = TryLoad<T>(normalizedPath, options, isValid);
                if (primary.Status == AtomicJsonLoadStatus.Loaded)
                {
                    return primary;
                }

                var backup = TryLoad<T>(GetBackupPath(normalizedPath), options, isValid);
                if (backup.Status == AtomicJsonLoadStatus.Loaded)
                {
                    return backup;
                }

                var status = primary.Status == AtomicJsonLoadStatus.Unavailable
                    || backup.Status == AtomicJsonLoadStatus.Unavailable
                        ? AtomicJsonLoadStatus.Unavailable
                        : primary.Status == AtomicJsonLoadStatus.Invalid
                            || backup.Status == AtomicJsonLoadStatus.Invalid
                                ? AtomicJsonLoadStatus.Invalid
                                : AtomicJsonLoadStatus.Missing;
                return new AtomicJsonLoadResult<T>(status);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    public static bool Save<T>(string filePath, T value, JsonSerializerOptions options)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        lock (GetPathLock(normalizedPath))
        {
            using var mutex = TryCreateAcquiredMutex(normalizedPath);
            if (mutex is null)
            {
                return false;
            }

            try
            {
                try
                {
                    var directory = Path.GetDirectoryName(normalizedPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    ScavengeStaleTemporaryFiles(normalizedPath);
                    var json = JsonSerializer.Serialize(value, options);
                    Commit(normalizedPath, json);
                    Commit(GetBackupPath(normalizedPath), json);
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }

    internal static string GetMutexName(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var pathBytes = Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant());
        var hash = Convert.ToHexString(SHA256.HashData(pathBytes));
        return $"Local\\PulseMeter.AtomicJsonFileStore.{hash}";
    }

    private static object GetPathLock(string normalizedPath)
    {
        return PathLocks.GetOrAdd(normalizedPath, static _ => new object());
    }

    private static string GetBackupPath(string normalizedPath)
    {
        return normalizedPath + ".bak";
    }

    private static bool TryAcquireMutex(Mutex mutex)
    {
        for (var attempt = 0; attempt < MutexWaitAttempts; attempt++)
        {
            try
            {
                if (mutex.WaitOne(MutexWaitMilliseconds))
                {
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }

        return false;
    }

    private static Mutex? TryCreateAcquiredMutex(string normalizedPath)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, GetMutexName(normalizedPath));
            if (TryAcquireMutex(mutex))
            {
                return mutex;
            }

            mutex.Dispose();
            return null;
        }
        catch (IOException)
        {
            mutex?.Dispose();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            mutex?.Dispose();
            return null;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            mutex?.Dispose();
            return null;
        }
    }

    private static AtomicJsonLoadResult<T> TryLoad<T>(
        string filePath,
        JsonSerializerOptions options,
        Func<T, bool>? isValid)
    {
        for (var attempt = 0; attempt < ReadAttempts; attempt++)
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>(File.ReadAllText(filePath), options);
                return value is not null && (isValid?.Invoke(value) ?? true)
                    ? new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Loaded, value)
                    : new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Invalid);
            }
            catch (FileNotFoundException)
            {
                return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Missing);
            }
            catch (DirectoryNotFoundException)
            {
                return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Missing);
            }
            catch (JsonException)
            {
                return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Invalid);
            }
            catch (IOException) when (attempt < ReadAttempts - 1)
            {
                Thread.Sleep(MutexWaitMilliseconds);
            }
            catch (UnauthorizedAccessException) when (attempt < ReadAttempts - 1)
            {
                Thread.Sleep(MutexWaitMilliseconds);
            }
            catch (IOException)
            {
                return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Unavailable);
            }
            catch (UnauthorizedAccessException)
            {
                return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Unavailable);
            }
        }

        return new AtomicJsonLoadResult<T>(AtomicJsonLoadStatus.Unavailable);
    }

    private static void Commit(string filePath, string json)
    {
        var directory = Path.GetDirectoryName(filePath) ?? throw new IOException("JSON storage path has no directory.");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom, bufferSize: 4096, leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void ScavengeStaleTemporaryFiles(string normalizedPath)
    {
        ScavengeStaleTemporaryFiles(normalizedPath, Path.GetFileName(normalizedPath));
        ScavengeStaleTemporaryFiles(normalizedPath, Path.GetFileName(GetBackupPath(normalizedPath)));
    }

    private static void ScavengeStaleTemporaryFiles(string normalizedPath, string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(normalizedPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            var oldestAllowedWriteTime = DateTime.UtcNow - StaleTemporaryFileAge;
            foreach (var temporaryPath in Directory.EnumerateFiles(directory, $".{fileName}.*.tmp"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(temporaryPath) <= oldestAllowedWriteTime)
                    {
                        TryDelete(temporaryPath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
