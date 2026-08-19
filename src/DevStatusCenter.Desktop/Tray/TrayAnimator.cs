using System.Runtime.Versioning;
using DevStatusCenter.Domain.Enums;
using Forms = System.Windows.Forms;

namespace DevStatusCenter.Desktop.Tray;

/// <summary>Un cuadro de la cara: la rejilla y el color con el que se pinta.</summary>
internal sealed record FaceFrame(string[] Grid, uint Color);

/// <summary>Cuanto se le permite moverse al icono.</summary>
public enum TrayMotion
{
    /// <summary>Gags, parpadeo y todo lo funcional.</summary>
    Full,

    /// <summary>Solo lo que comunica algo: sincronizacion, novedad, aviso.</summary>
    Useful,

    /// <summary>Una sola imagen. Cambia de color y de expresion, pero no se mueve.</summary>
    Still
}

/// <summary>
/// Dueno de lo que se ve en la bandeja.
///
/// Animar aqui es reemplazar el icono muchas veces por segundo, y cada cuadro es un mapa de bits
/// y un handle nuevos. A 16x16 es barato pero no es gratis, asi que <b>nada corre en bucle sin
/// motivo</b>: cada animacion tiene un evento que la enciende y un final. En reposo hay un unico
/// icono y ningun temporizador de cuadros, que es lo que sostiene el compromiso de 0 % de CPU.
///
/// El unico temporizador que sigue vivo en reposo es el de la vida ociosa (parpadeo y gags), y
/// solo existe en <see cref="TrayMotion.Full"/>: dispara cada 25-70 s, no cada cuadro.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TrayAnimator : IDisposable
{
    private static readonly TimeSpan GagCooldown = TimeSpan.FromMinutes(12);

    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.Timer _frameTimer;
    private readonly Forms.Timer _idleLifeTimer;
    private readonly TimeProvider _timeProvider;

    private TrayMotion _motion;
    private PowerMode _mode = PowerMode.Normal;
    private decimal _budgetPercent;
    private bool _providersFailing;
    private bool _paymentDue;

    private Icon? _resting;
    private Icon[]? _frames;
    private string[][]? _frameGrids;
    private int _frameIndex;
    private bool _loops;
    private bool _syncing;
    private int _lastGagIndex = -1;
    private DateTimeOffset _lastGagAt = DateTimeOffset.MinValue;
    private bool _disposed;

    public TrayAnimator(Forms.NotifyIcon notifyIcon, TrayMotion motion, TimeProvider timeProvider)
    {
        _notifyIcon = notifyIcon;
        _motion = motion;
        _timeProvider = timeProvider;
        _frameTimer = new Forms.Timer { Interval = 130, Enabled = false };
        _frameTimer.Tick += OnFrameTick;
        _idleLifeTimer = new Forms.Timer { Interval = NextIdleInterval(), Enabled = false };
        _idleLifeTimer.Tick += OnIdleLifeTick;
        ApplyResting();
        UpdateIdleLifeTimer();
    }

    /// <summary>
    /// El cuadro que se esta viendo ahora mismo en la bandeja. La cara del popup se engancha aqui
    /// para mostrar exactamente lo mismo, en el mismo instante: no son dos caras parecidas, es la
    /// misma dibujada dos veces.
    /// </summary>
    public FaceFrame Current { get; private set; } = new(TrayArt.EyesOpen, TrayArt.Accent(PowerMode.Normal, 0m));

    public event EventHandler<FaceFrame>? FrameChanged;

    public TrayMotion Motion
    {
        get => _motion;
        set
        {
            if (_motion == value)
            {
                return;
            }

            _motion = value;
            if (value == TrayMotion.Still)
            {
                StopPlayback();
            }

            UpdateIdleLifeTimer();
        }
    }

    /// <summary>Estado permanente: expresion, color y medidor. No es una animacion.</summary>
    public void SetState(PowerMode mode, decimal budgetPercent, bool providersFailing, bool paymentDue)
    {
        var changed = _mode != mode
            || _budgetPercent != budgetPercent
            || _providersFailing != providersFailing
            || _paymentDue != paymentDue;
        _mode = mode;
        _budgetPercent = budgetPercent;
        _providersFailing = providersFailing;
        _paymentDue = paymentDue;
        if (changed)
        {
            ApplyResting();
        }
    }

    /// <summary>El modo de energia llega por su propio evento, sin snapshot de por medio.</summary>
    public void SetPowerMode(PowerMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        ApplyResting();
    }

    /// <summary>Barrido mientras el scheduler consulta a los proveedores.</summary>
    public void SetSyncing(bool syncing)
    {
        if (_syncing == syncing)
        {
            return;
        }

        _syncing = syncing;
        if (!syncing)
        {
            StopPlayback();
            return;
        }

        if (_motion == TrayMotion.Still)
        {
            return;
        }

        var frames = new List<string[]>();
        for (var head = -2; head < 12; head++)
        {
            frames.Add(TrayArt.Merge(Face(), TrayArt.MeterSweep(head, _paymentDue)));
        }

        Play(frames.ToArray(), intervalMs: 110, loops: true);
    }

    /// <summary>
    /// Una mirada rapida cuando un refresh trajo algo distinto. Distingue "revise y todo igual"
    /// de "hay novedades", que es la diferencia que el icono no sabia contar.
    /// </summary>
    public void PlayGlance()
    {
        if (_motion == TrayMotion.Still || _syncing)
        {
            return;
        }

        var meter = TrayArt.Meter(_budgetPercent, _paymentDue);
        Play(
            [
                TrayArt.Merge(TrayArt.EyesRight, meter),
                TrayArt.Merge(TrayArt.EyesRight, meter),
                TrayArt.Merge(TrayArt.EyesLeft, meter),
                TrayArt.Merge(TrayArt.EyesLeft, meter),
                TrayArt.Merge(TrayArt.EyesOpen, meter),
            ],
            intervalMs: 120,
            loops: false);
    }

    /// <summary>
    /// Tres pulsos y se queda quieto. Es el unico momento en que el icono pide que lo mires, y
    /// por eso interrumpe lo que este corriendo.
    /// </summary>
    public void PlayAlert()
    {
        if (_motion == TrayMotion.Still)
        {
            return;
        }

        // Sin boca, el pulso es la cara apareciendo y desapareciendo sobre el medidor, que se
        // queda quieto: lo que parpadea es la atencion, no el dato.
        var meter = TrayArt.Meter(_budgetPercent, _paymentDue);
        var open = TrayArt.Merge(TrayArt.EyesOpen, meter);
        Play([open, open, meter, open, open, meter, open, open, open], intervalMs: 130, loops: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frameTimer.Tick -= OnFrameTick;
        _idleLifeTimer.Tick -= OnIdleLifeTick;
        _frameTimer.Dispose();
        _idleLifeTimer.Dispose();
        DisposeFrames();
        _resting?.Dispose();
    }

    // ---------------------------------------------------------------------------------------

    private void OnIdleLifeTick(object? sender, EventArgs e)
    {
        _idleLifeTimer.Interval = NextIdleInterval();
        if (_motion != TrayMotion.Full || _frames is not null || _syncing)
        {
            return;
        }

        // Un gag como mucho cada doce minutos; el resto de las veces solo parpadea. Y nunca
        // mientras el presupuesto este en zona de aviso: un chiste encima de una advertencia es
        // lo unico que puede lograr que la advertencia no se lea.
        var now = _timeProvider.GetUtcNow();
        var calm = _budgetPercent < 85m && !_providersFailing && _mode == PowerMode.Normal;
        if (calm && now - _lastGagAt >= GagCooldown)
        {
            _lastGagAt = now;
            PlayGag();
            return;
        }

        PlayBlink();
    }

    private void PlayBlink()
    {
        var meter = TrayArt.Meter(_budgetPercent, _paymentDue);
        Play(
            [
                TrayArt.Merge(TrayArt.EyesBlink, meter),
                TrayArt.Merge(TrayArt.EyesOpen, meter),
            ],
            intervalMs: 110,
            loops: false);
    }

    private void PlayGag()
    {
        // Nunca el mismo dos veces seguidas: repetir el chiste es lo que lo mata.
        int index;
        do
        {
            index = Random.Shared.Next(TrayArt.Gags.Length);
        }
        while (TrayArt.Gags.Length > 1 && index == _lastGagIndex);
        _lastGagIndex = index;

        var gag = TrayArt.Gags[index];
        var meter = TrayArt.Meter(_budgetPercent, _paymentDue);
        var frames = new List<string[]>();

        // Entra desde abajo, se queda algo mas de medio segundo y baja. El medidor no se mueve.
        foreach (var dy in (int[])[10, 6, 3, 0])
        {
            frames.Add(TrayArt.Merge(TrayArt.Shift(gag, dy: dy), meter));
        }

        var hold = ReferenceEquals(gag, TrayArt.Coffee)
            ? (string[][])[TrayArt.Coffee, TrayArt.CoffeeSteam, TrayArt.Coffee, TrayArt.CoffeeSteam]
            : [gag, gag, gag, gag];
        foreach (var frame in hold)
        {
            frames.Add(TrayArt.Merge(frame, meter));
        }

        foreach (var dy in (int[])[3, 6, 10])
        {
            frames.Add(TrayArt.Merge(TrayArt.Shift(gag, dy: dy), meter));
        }

        Play(frames.ToArray(), intervalMs: 140, loops: false);
    }

    private void Play(string[][] grids, int intervalMs, bool loops)
    {
        DisposeFrames();
        var color = CurrentColor();
        var frames = new Icon[grids.Length];
        for (var i = 0; i < grids.Length; i++)
        {
            frames[i] = TrayIconFactory.Create(grids[i], color);
        }

        _frames = frames;
        _frameGrids = grids;
        _frameIndex = 0;
        _loops = loops;
        _frameTimer.Interval = intervalMs;
        _notifyIcon.Icon = frames[0];
        Publish(grids[0], color);
        _frameTimer.Start();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        var frames = _frames;
        if (frames is null)
        {
            _frameTimer.Stop();
            return;
        }

        _frameIndex++;
        if (_frameIndex >= frames.Length)
        {
            if (!_loops)
            {
                StopPlayback();
                return;
            }

            _frameIndex = 0;
        }

        _notifyIcon.Icon = frames[_frameIndex];
        var grids = _frameGrids;
        if (grids is not null)
        {
            Publish(grids[_frameIndex], Current.Color);
        }
    }

    private void StopPlayback()
    {
        _frameTimer.Stop();
        ApplyResting();
        DisposeFrames();
    }

    private void DisposeFrames()
    {
        var frames = _frames;
        _frames = null;
        _frameGrids = null;
        if (frames is null)
        {
            return;
        }

        foreach (var frame in frames)
        {
            frame.Dispose();
        }
    }

    private void ApplyResting()
    {
        var grid = TrayArt.Merge(Face(), TrayArt.Meter(_budgetPercent, _paymentDue));
        var color = CurrentColor();
        var replacement = TrayIconFactory.Create(grid, color);

        // Se asigna antes de liberar el anterior: soltarlo mientras Windows aun lo usa deja el
        // hueco en blanco.
        _notifyIcon.Icon = replacement;
        _resting?.Dispose();
        _resting = replacement;
        Publish(grid, color);
    }

    private void Publish(string[] grid, uint color)
    {
        Current = new FaceFrame(grid, color);
        FrameChanged?.Invoke(this, Current);
    }

    /// <summary>La expresion es estado, no animacion: puede durar horas y no debe moverse.</summary>
    private string[] Face() => _mode == PowerMode.Paused
        ? TrayArt.EyesSleep
        : _providersFailing
            ? TrayArt.EyesDead
            : TrayArt.EyesOpen;

    private uint CurrentColor() => TrayArt.Accent(_mode, _budgetPercent);

    private void UpdateIdleLifeTimer()
    {
        if (_motion == TrayMotion.Full)
        {
            _idleLifeTimer.Start();
        }
        else
        {
            _idleLifeTimer.Stop();
        }
    }

    private static int NextIdleInterval() => Random.Shared.Next(25_000, 70_000);
}
