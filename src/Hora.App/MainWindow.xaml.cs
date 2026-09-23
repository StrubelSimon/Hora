using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Hora.App.ViewModels;
using Hora.App.Views;

namespace Hora.App;

public partial class MainWindow : Window
{
    /// <summary>
    /// Die Hülle, an deren Ereignis gerade gehorcht wird. Festgehalten, um die Anmeldung beim
    /// nächsten Wechsel des Datenkontexts wieder zu lösen; sonst hinge das Fenster an zwei Hüllen.
    /// </summary>
    private MainViewModel? _boundViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Der Datenkontext wird erst nach dem Konstruktor gesetzt (App.ShowMainWindow); hier steht
        // er also noch nicht zur Verfügung.
        DataContextChanged += OnDataContextChanged;

        // Ein randloses Fenster mit WindowChrome maximiert sich sonst auf die volle Bildschirmgröße
        // des Monitors statt auf dessen Arbeitsbereich und verdeckt dabei die Taskleiste.
        //
        // Früher stand hier eine Begrenzung von MaxHeight/MaxWidth auf SystemParameters.WorkArea.
        // Das war gleich doppelt falsch: SystemParameters.WorkArea liefert ausschließlich den
        // Arbeitsbereich des PRIMÄREN Monitors, und die Grenze galt DAUERHAFT – auf einem größeren
        // Zweitbildschirm ließ sich das Fenster deshalb nie auf volle Größe ziehen, auch nicht von
        // Hand.
        //
        // Stattdessen die übliche Lösung für randlose WPF-Fenster: WM_GETMINMAXINFO in einem
        // HwndSource-Hook behandeln. Windows fragt dort nach Position und Größe des maximierten
        // Fensters, und zwar jedes Mal neu – der Arbeitsbereich wird also für genau den Monitor
        // ermittelt, auf dem das Fenster gerade liegt. Außerhalb des Maximierens gibt es keine
        // Grenze mehr.
        SourceInitialized += OnSourceInitialized;

        StateChanged += (_, _) => UpdateMaximizeRestoreGlyph();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_boundViewModel is not null)
        {
            _boundViewModel.AsanaConnectRequested -= OnAsanaConnectRequested;
        }

        _boundViewModel = e.NewValue as MainViewModel;

        if (_boundViewModel is not null)
        {
            _boundViewModel.AsanaConnectRequested += OnAsanaConnectRequested;
        }
    }

    /// <summary>
    /// Die Profilseite will Asana verbinden. Das Fenster gehört der Hülle nicht — es zu öffnen ist
    /// Sache der Ansicht, damit das MainViewModel ohne WPF-Typen prüfbar bleibt.
    /// </summary>
    private void OnAsanaConnectRequested(ProfileViewModel profile)
    {
        if (_boundViewModel?.CreateAsanaConnectViewModel() is not { } connectViewModel)
        {
            return;
        }

        var window = new AsanaConnectWindow { Owner = this, DataContext = connectViewModel };
        connectViewModel.Connected += window.Close;
        window.ShowDialog();

        // Nach dem Schließen den Stand unbedingt neu lesen: Ob das Fenster verbunden oder nur
        // abgebrochen hat, weiß die Ansicht nicht. Ob daraus ein Neuabruf folgt, entscheidet
        // RefreshAsanaState anhand des tatsächlichen Unterschieds — diese Entscheidung gehört ins
        // ViewModel und nicht hierher.
        profile.RefreshAsanaState();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        ApplyRoundedWindowCorners(handle);
    }

    /// <summary>Rundet die vier Fensterecken leicht ab.</summary>
    /// <remarks>
    /// Ein Fenster mit WindowStyle="None" hat keinen vom System gezeichneten Rahmen und bekommt
    /// deshalb auch nicht von selbst die abgerundeten Ecken, die Windows 11 sonst jedem Fenster
    /// gibt – es bleibt rechtwinklig, während alles daneben rund ist.
    ///
    /// WindowChrome.CornerRadius ist hier nicht der richtige Hebel: Der Wert wirkt nur bei
    /// AllowsTransparency="True", und diese Betriebsart kostet die Hardwarebeschleunigung des
    /// ganzen Fensters. Stattdessen der vom Fenstermanager selbst vorgesehene Weg über
    /// DwmSetWindowAttribute. Der Fenstermanager beschneidet das Fenster dann außerhalb von WPF,
    /// weshalb Inhalt bis in die Ecken sauber abgeschnitten wird und der Schlagschatten des
    /// Systems der neuen Form folgt.
    ///
    /// Gewählt ist RoundSmall (rund 4 Bildpunkte), nicht Round (rund 8): ausdrücklich nur eine
    /// leichte Rundung.
    /// </remarks>
    private static void ApplyRoundedWindowCorners(IntPtr hwnd)
    {
        var preference = (int)WindowCornerPreference.RoundSmall;

        // Rückgabewert bewusst nicht ausgewertet: Vor Windows 11 kennt der Fenstermanager das
        // Attribut nicht und antwortet mit E_INVALIDARG. Das Fenster bleibt dort eckig – genau
        // so, wie es auf Windows 10 auch jedes andere Fenster ist.
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    private static IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkArea(hwnd, lParam);
        }

        // handled bleibt bewusst false. WPF trägt in dieselbe Struktur anschließend noch
        // ptMinTrackSize und ptMaxTrackSize aus MinWidth/MinHeight/MaxWidth/MaxHeight ein; hier
        // werden ausschließlich ptMaxPosition und ptMaxSize gesetzt, die WPF nicht anfasst. Beide
        // Seiten kommen sich damit in beliebiger Reihenfolge nicht in die Quere, und die
        // Mindestgröße 860 × 560 aus MainWindow.xaml bleibt wirksam.
        return IntPtr.Zero;
    }

    /// <summary>
    /// Trägt in die MINMAXINFO-Struktur den Arbeitsbereich des Monitors ein, auf dem das Fenster
    /// gerade liegt.
    /// </summary>
    /// <remarks>
    /// Zur Bildpunktdichte: Sowohl MINMAXINFO als auch die Rechtecke aus GetMonitorInfo sind
    /// GERÄTEPIXEL im selben Koordinatensystem. Die ganze Rechnung bleibt deshalb in Gerätepixeln
    /// und rechnet an keiner Stelle in geräteunabhängige Einheiten (DIP) um – genau das macht sie
    /// unter unterschiedlicher Skalierung je Monitor richtig. Eine Umrechnung über
    /// CompositionTarget.TransformToDevice wäre hier sogar schädlich, weil sie die Skalierung des
    /// aktuellen Fensters auf Werte anwenden würde, die bereits zum Zielmonitor gehören. Der alte
    /// Weg über MaxHeight/MaxWidth war das Gegenteil: dort vermischte sich der in DIP gemessene
    /// Arbeitsbereich des Primärmonitors mit einem Fenster, das auf einem anders skalierten
    /// Zweitmonitor stehen konnte.
    /// </remarks>
    private static void ApplyMonitorWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        // ptMaxPosition ist relativ zur linken oberen Ecke des Monitors, nicht zum virtuellen
        // Bildschirm – deshalb die Differenz zwischen Arbeitsbereich und Monitorrechteck.
        info.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
        info.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
        info.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
        info.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;

        Marshal.StructureToPtr(info, lParam, fDeleteOld: true);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Zeigt je nach Fensterzustand das Maximieren- oder das Wiederherstellen-Symbol.</summary>
    private void UpdateMaximizeRestoreGlyph()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeRestoreGlyph.Data = Geometry.Parse(isMaximized
            ? "M2,0 L10,0 L10,8 L8,8 M0,2 L8,2 L8,10 L0,10 Z"
            : "M0,0 L10,0 L10,10 L0,10 Z");
        MaximizeRestoreButton.ToolTip = isMaximized ? "Wiederherstellen" : "Maximieren";
    }

    // ===================== Interop =====================

    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>Entspricht MONITORINFO. <see cref="Size"/> muss vor dem Aufruf gesetzt sein.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public int Flags;
    }

    /// <summary>Entspricht MINMAXINFO. Alle Werte in Gerätepixeln.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    /// <summary>Entspricht DWMWA_WINDOW_CORNER_PREFERENCE.</summary>
    private const int DwmwaWindowCornerPreference = 33;

    /// <summary>Entspricht DWM_WINDOW_CORNER_PREFERENCE.</summary>
    private enum WindowCornerPreference
    {
        /// <summary>Der Fenstermanager entscheidet.</summary>
        Default = 0,

        /// <summary>Nie abrunden.</summary>
        DoNotRound = 1,

        /// <summary>Volle Rundung, rund 8 Bildpunkte.</summary>
        Round = 2,

        /// <summary>Leichte Rundung, rund 4 Bildpunkte.</summary>
        RoundSmall = 3,
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
