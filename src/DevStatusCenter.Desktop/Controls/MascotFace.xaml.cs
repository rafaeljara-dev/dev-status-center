using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using UserControl = System.Windows.Controls.UserControl;

namespace DevStatusCenter.Desktop.Controls;

/// <summary>
/// La mascota de la primera pestana: un circulo con ojos que parpadea y, cada tantos segundos,
/// saca una laptop, un celular o un cafe.
///
/// Los gags son deliberadamente ajenos al estado de la aplicacion. El estado lo comunican el
/// icono del area de notificaciones, la linea monoespaciada y el medidor de presupuesto; que la
/// carita tambien lo hiciera obligaria a leer dos cosas para saber una.
///
/// Todo el movimiento vive en un unico Storyboard que se detiene al ocultarse el popup: con la
/// ventana escondida la aplicacion no puede gastar CPU (NFR-004). Si el sistema tiene desactivadas
/// las animaciones, no arranca nunca y queda una carita fija.
/// </summary>
public partial class MascotFace : UserControl
{
    private Storyboard? _life;

    public MascotFace()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        Unloaded += (_, _) => StopLife();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            StartLife();
        }
        else
        {
            StopLife();
        }
    }

    private void StartLife()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        _life ??= (Storyboard)FindResource("Life");

        // isControllable: true es lo que permite detenerlo despues; sin eso, Stop no haria nada.
        _life.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);
    }

    private void StopLife() => _life?.Stop(this);
}
