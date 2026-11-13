using System.ServiceProcess;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace ParentalControl.Service;

public sealed class ParentalControlServiceLifetime : WindowsServiceLifetime
{
    private readonly SessionTracker _sessionTracker;
    private readonly TelegramNotifier _notifier;
    private readonly Serilog.ILogger _logger;
    private int _shutdownNotified;

    public ParentalControlServiceLifetime(
        IHostEnvironment environment,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory,
        IOptions<HostOptions> optionsAccessor,
        IOptions<WindowsServiceLifetimeOptions> windowsServiceOptions,
        SessionTracker sessionTracker,
        TelegramNotifier notifier,
        Serilog.ILogger logger)
        : base(environment, applicationLifetime, loggerFactory, optionsAccessor, windowsServiceOptions)
    {
        CanHandleSessionChangeEvent = true;
        CanHandlePowerEvent = true;
        CanShutdown = true;
        _sessionTracker = sessionTracker;
        _notifier = notifier;
        _logger = logger;
    }

    protected override void OnSessionChange(SessionChangeDescription changeDescription)
    {
        base.OnSessionChange(changeDescription);

        try
        {
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    _sessionTracker.OnUserLogon(changeDescription.SessionId);
                    break;
                case SessionChangeReason.SessionLogoff:
                    _sessionTracker.OnUserLogoff(changeDescription.SessionId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling session change: {Reason}, SessionId: {SessionId}",
                changeDescription.Reason, changeDescription.SessionId);
        }
    }

    protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
    {
        try
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.Suspend:
                    _sessionTracker.OnSleep();
                    break;
                case PowerBroadcastStatus.ResumeSuspend:
                case PowerBroadcastStatus.ResumeAutomatic:
                    _sessionTracker.OnWake();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error handling power event: {PowerStatus}", powerStatus);
        }

        return base.OnPowerEvent(powerStatus);
    }

    protected override void OnShutdown()
    {
        TryNotifyShutdown("System Shutdown");
        base.OnShutdown();
    }

    protected override void OnStop()
    {
        TryNotifyShutdown("Service Stop");
        base.OnStop();
    }

    private void TryNotifyShutdown(string reason)
    {
        if (Interlocked.Exchange(ref _shutdownNotified, 1) == 0)
        {
            try
            {
                _logger.Information("ParentalControl service terminating ({Reason}); sending shutdown notification", reason);
                _notifier.NotifyShutdown();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not send shutdown notification");
            }
        }
    }
}
