using System;
using System.Threading;

namespace Homebase.Application
{
    /// <summary>
    /// Fires time triggers and polls network devices every 15 seconds. Overlapping ticks are skipped, and errors are
    /// reported to <paramref name="onError"/> instead of killing the timer.
    /// </summary>
    public sealed class AutomationScheduler : IDisposable
    {
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

        private readonly AutomationService _automations;
        private readonly Action<Exception> _onError;
        private readonly Timer _timer;
        private int _running;

        public AutomationScheduler(AutomationService automations, Action<Exception> onError)
        {
            _automations = automations;
            _onError = onError ?? (_ => { });
            _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start() => _timer.Change(TimeSpan.Zero, Interval);

        public void Dispose() => _timer.Dispose();

        private async void Tick()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                return;
            }

            try
            {
                await _automations.PollDevicesAsync().ConfigureAwait(false);
                await _automations.TickAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _onError(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }
}
