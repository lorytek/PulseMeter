using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace PulseMeter.Platform.Persistence;

internal static class AtomicJsonFileStore
{
    private const int MutexWaitMilliseconds = 100;
    private const int MutexWaitAttempts = 20;
    private const int ReadAttempts = 3;
    private static readonly TimeSpan StaleTemporaryFileAge = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static T? Load<T>(string filePath, JsonSerializerOptions options)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        lock (GetPathLock(normalizedPath))
        {
            using var mutex = TryCreateAcquiredMutex(normalizedPath);
            if (mutex is null)
            {
                return default;
            }

            try
            {
                ScavengeStaleTemporaryFiles(normalizedPath);
                return TryLoad<T>(normalizedPath, options, out var primary)
                    ? primary
                    : TryLoad<T>(GetBackupPath(normalizedPath), options, out var backup)
                        ? backup
                        : default;
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

    private static bool TryLoad<T>(string filePath, JsonSerializerOptions options, out T? value)
    {
        for (var attempt = 0; attempt < ReadAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    value = default;
                    return false;
                }

                value = JsonSerializer.Deserialize<T>(File.ReadAllText(filePath), options);
                return value is not null;
            }
            catch (JsonException)
            {
                value = default;
                return false;
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
                value = default;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                value = default;
                return false;
            }
        }

        value = default;
        return false;
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
