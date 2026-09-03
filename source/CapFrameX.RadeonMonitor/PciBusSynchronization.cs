using System.Security;

namespace CapFrameX.RadeonMonitor
{
    internal static class PciBusSynchronization
    {
        private const int TimeoutMilliseconds = 5000;
        private const string MutexName = @"Global\Access_PCI";

        private static readonly Mutex? AccessMutex = CreateOrOpenMutex();

        public static T Execute<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (AccessMutex is null)
            {
                return action();
            }

            bool acquired;
            try
            {
                acquired = AccessMutex.WaitOne(TimeoutMilliseconds, exitContext: false);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            catch (InvalidOperationException)
            {
                acquired = false;
            }

            if (!acquired)
            {
                throw new TimeoutException(
                    $"Timed out after {TimeoutMilliseconds} ms waiting for {MutexName}.");
            }

            try
            {
                return action();
            }
            finally
            {
                AccessMutex.ReleaseMutex();
            }
        }

        private static Mutex? CreateOrOpenMutex()
        {
            try
            {
                return new Mutex(initiallyOwned: false, name: MutexName);
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    return Mutex.OpenExisting(MutexName);
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }
            catch (SecurityException)
            {
                return null;
            }
        }
    }
}
