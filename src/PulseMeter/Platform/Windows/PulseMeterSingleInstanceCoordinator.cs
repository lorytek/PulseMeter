using PulseMeter.Platform.Diagnostics;

namespace PulseMeter.Platform.Windows;

internal sealed class PulseMeterSingleInstanceCoordinator : IDisposable
{
    private const string DefaultScopeName = "Local\\PulseMeter.SingleInstance.v1";
    // Windows can take a short time to surface an abandoned named mutex after
    // its owning process or thread exits. A secondary launch must wait through
    // that handoff or it can signal a primary instance that no longer exists.
    private static readonly TimeSpan OwnershipAcquireTimeout = TimeSpan.FromMilliseconds(500);
    private readonly Mutex? _ownershipMutex;
    private readonly Semaphore? _legacyOwnershipSemaphore;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _listenerCancellation = new();
    private Task? _listenerTask;
    private int _disposed;

    private PulseMeterSingleInstanceCoordinator(
        Mutex? ownershipMutex,
        Semaphore? legacyOwnershipSemaphore,
        EventWaitHandle activationEvent,
        bool isPrimary)
    {
        _ownershipMutex = ownershipMutex;
        _legacyOwnershipSemaphore = legacyOwnershipSemaphore;
        _activationEvent = activationEvent;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static PulseMeterSingleInstanceCoordinator Acquire(string scopeName = DefaultScopeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeName);

        Mutex? ownershipMutex = null;
        Semaphore? legacyOwnershipSemaphore = null;
        bool isPrimary;
        var ownershipName = scopeName + ".Owner";
        try
        {
            ownershipMutex = new Mutex(initiallyOwned: false, ownershipName);
            isPrimary = TryAcquireOwnership(ownershipMutex);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Builds before the mutex migration used a semaphore with this name. Keep
            // update-time interoperability so the new process activates the running
            // legacy instance instead of crashing or opening a duplicate window.
            ownershipMutex?.Dispose();
            ownershipMutex = null;
            legacyOwnershipSemaphore = new Semaphore(1, 1, ownershipName);
            isPrimary = legacyOwnershipSemaphore.WaitOne(0);
        }

        try
        {
            var activationEvent = new EventWaitHandle(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: scopeName + ".Activate");
            return new PulseMeterSingleInstanceCoordinator(
                ownershipMutex,
                legacyOwnershipSemaphore,
                activationEvent,
                isPrimary);
        }
        catch
        {
            if (isPrimary)
            {
                ReleaseOwnership(ownershipMutex, legacyOwnershipSemaphore);
            }

            ownershipMutex?.Dispose();
            legacyOwnershipSemaphore?.Dispose();
            throw;
        }
    }

    public void StartListening(Action activationRequested)
    {
        ArgumentNullException.ThrowIfNull(activationRequested);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary PulseMeter instance can listen for activation requests.");
        }

        if (_listenerTask is not null)
        {
            throw new InvalidOperationException("The PulseMeter activation listener has already started.");
        }

        _listenerTask = Task.Factory.StartNew(
            () => ListenForActivation(activationRequested),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void SignalPrimary()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (IsPrimary)
        {
            throw new InvalidOperationException("The primary PulseMeter instance cannot signal itself.");
        }

        _activationEvent.Set();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _listenerCancellation.Cancel();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException exception)
        {
            PrivacySafeDiagnostics.WriteFailure("single-instance activation listener shutdown failed", exception);
        }

        _activationEvent.Dispose();
        _listenerCancellation.Dispose();

        if (IsPrimary)
        {
            try
            {
                ReleaseOwnership(_ownershipMutex, _legacyOwnershipSemaphore);
            }
            catch (Exception exception) when (exception is ApplicationException or SemaphoreFullException)
            {
                PrivacySafeDiagnostics.WriteFailure("single-instance ownership release failed", exception);
            }
        }

        _ownershipMutex?.Dispose();
        _legacyOwnershipSemaphore?.Dispose();
    }

    private static bool TryAcquireOwnership(Mutex ownershipMutex)
    {
        try
        {
            return ownershipMutex.WaitOne(OwnershipAcquireTimeout);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    private static void ReleaseOwnership(Mutex? ownershipMutex, Semaphore? legacyOwnershipSemaphore)
    {
        if (ownershipMutex is not null)
        {
            ownershipMutex.ReleaseMutex();
            return;
        }

        legacyOwnershipSemaphore?.Release();
    }

    private void ListenForActivation(Action activationRequested)
    {
        var waitHandles = new WaitHandle[]
        {
            _activationEvent,
            _listenerCancellation.Token.WaitHandle
        };

        while (WaitHandle.WaitAny(waitHandles) == 0)
        {
            try
            {
                activationRequested();
            }
            catch (Exception exception)
            {
                PrivacySafeDiagnostics.WriteFailure("single-instance activation failed", exception);
            }
        }
    }
}
