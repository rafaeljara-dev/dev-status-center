using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Application.Power;

public sealed class PowerManager
{
    private readonly object _gate = new();
    private PowerMode _mode;

    public PowerManager(PowerMode initialMode = PowerMode.Normal)
    {
        _mode = initialMode;
    }

    public event EventHandler<PowerMode>? ModeChanged;

    public PowerMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    public bool AllowsBackgroundActivity => Mode is PowerMode.Normal or PowerMode.Eco;

    public void SetMode(PowerMode mode)
    {
        lock (_gate)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
        }

        ModeChanged?.Invoke(this, mode);
    }
}

