namespace ShortcutOverlay.Helpers;

/// <summary>
/// Prevents multiple instances of the application from running simultaneously.
/// Uses a named Mutex.
/// </summary>
public static class SingleInstanceGuard
{
    private static Mutex? _mutex;
    private const string MutexName = "ShortcutOverlay_SingleInstance_7F8D4E1B";

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        return true;
    }

    public static void Release()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }
}
