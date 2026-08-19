namespace DevStatusCenter.Desktop.Windows;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceGuard(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, "Local\\DevStatusCenter.Application", out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstanceGuard(mutex);
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

