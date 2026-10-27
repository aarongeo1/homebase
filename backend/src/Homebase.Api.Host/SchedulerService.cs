using System.Threading;
using System.Threading.Tasks;
using Homebase.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Homebase.Api.Host
{
    /// <summary>Runs the automation scheduler (time triggers, forMinutes countdowns, device polling) for the app's lifetime.</summary>
    internal sealed class SchedulerService : IHostedService
    {
        private readonly AutomationScheduler _scheduler;

        public SchedulerService(AutomationService automations, ILogger<SchedulerService> logger)
        {
            _scheduler = new AutomationScheduler(automations, ex => logger.LogWarning(ex, "Scheduler tick failed"));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _scheduler.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _scheduler.Dispose();
            return Task.CompletedTask;
        }
    }
}
