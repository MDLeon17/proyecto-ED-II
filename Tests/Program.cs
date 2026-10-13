using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UfgEnvios.Data;
using UfgEnvios.Models;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length is < 1 or > 2) throw new ArgumentException("Indica la ruta de imagen ejemplo.jpg y, opcionalmente, una vista previa PNG.");
        var nodes = DefaultMapData.GetDefaultHubs();
        var image = new BitmapImage(new Uri(Path.GetFullPath(args[0])));
        var regions = new MapRegionIndex(image, nodes);
        var app = new UfgEnvios.App();
        app.InitializeComponent();
        var packagedImage = new BitmapImage(new Uri("pack://application:,,,/UfgEnvios;component/imagen%20ejemplo.jpg", UriKind.Absolute));
        _ = new MapRegionIndex(packagedImage, nodes);
        foreach (var node in nodes)
        {
            var mask = regions.CreateOverlay(node, null, false)!;
            var pixel = new byte[4];
            mask.CopyPixels(new Int32Rect(node.RegionX, node.RegionY, 1, 1), pixel, 4, 0);
            if (pixel[3] == 0) throw new Exception($"Sin sombreado: {node.Name}");
            foreach (var other in nodes.Where(other => other.Id != node.Id))
            {
                mask.CopyPixels(new Int32Rect(other.RegionX, other.RegionY, 1, 1), pixel, 4, 0);
                if (pixel[3] != 0) throw new Exception($"Contornos mezclados: {node.Name} y {other.Name}");
            }
        }
        var selectedOnly = regions.CreateOverlay(nodes[0], nodes[1], false)!;
        var complete = regions.CreateOverlay(nodes[0], nodes[1], true)!;
        var third = nodes[2];
        var sample = new byte[4];
        selectedOnly.CopyPixels(new Int32Rect(third.RegionX, third.RegionY, 1, 1), sample, 4, 0);
        if (sample[3] != 0) throw new Exception("El sombreado de selección alcanzó otro municipio.");
        complete.CopyPixels(new Int32Rect(third.RegionX, third.RegionY, 1, 1), sample, 4, 0);
        if (sample[3] == 0) throw new Exception("La red completa no atenúa otros municipios.");
        var window = new UfgEnvios.MainWindow();
        var layout = (FrameworkElement)window.Content;
        layout.Measure(new Size(1440, 900));
        layout.Arrange(new Rect(0, 0, 1440, 900));
        layout.UpdateLayout();
        window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var readyFrame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => readyFrame.Continue = false));
        Dispatcher.PushFrame(readyFrame);
        var compare = (RadioButton)window.FindName("RbCompare")!;
        var singleHost = (Grid)window.FindName("SingleMapHost")!;
        var compareHost = (Grid)window.FindName("CompareMapsHost")!;
        ((RadioButton)window.FindName("RbBFS")!).IsChecked = false;
        compare.IsChecked = true;
        if (singleHost.Visibility != Visibility.Collapsed || compareHost.Visibility != Visibility.Visible)
            throw new Exception("La comparativa no mostró los dos mapas.");
        if (((Canvas)window.FindName("BfsMapCanvas")!).Children.Count < 24 ||
            ((Canvas)window.FindName("DfsMapCanvas")!).Children.Count < 24)
            throw new Exception("Falta la red en uno de los mapas de la comparativa.");
        var destination = (ComboBox)window.FindName("CboDestination")!;
        destination.SelectedItem = destination.Items.Cast<HubNode>().First(node => node.Id == "SAL");
        ((Slider)window.FindName("SliderSpeed")!).Value = 4;
        ((Button)window.FindName("BtnDispatch")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var bfsStatus = (TextBlock)window.FindName("TxtBfsMapStatus")!;
        var dfsStatus = (TextBlock)window.FindName("TxtDfsMapStatus")!;
        if (!bfsStatus.Text.StartsWith("0/") || !dfsStatus.Text.StartsWith("0/"))
            throw new Exception("Las animaciones BFS y DFS no arrancaron juntas.");
        var frame = new DispatcherFrame();
        var timedOut = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        var deadline = DateTime.UtcNow.AddSeconds(8);
        timer.Tick += (_, _) =>
        {
            if (bfsStatus.Text == "Entregado" && dfsStatus.Text == "Entregado") frame.Continue = false;
            else if (DateTime.UtcNow >= deadline) { timedOut = true; frame.Continue = false; }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (timedOut || ((ProgressBar)window.FindName("BfsProgress")!).Value != 100 ||
            ((ProgressBar)window.FindName("DfsProgress")!).Value != 100)
            throw new Exception("La comparativa en paralelo no terminó correctamente.");
        if (args.Length == 2)
        {
            layout.Measure(new Size(1440, 900));
            layout.Arrange(new Rect(0, 0, 1440, 900));
            layout.UpdateLayout();
            if (compare.IsChecked != true)
                throw new Exception("La opción de comparativa perdió su selección visual.");
            var snapshot = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32);
            snapshot.Render(layout);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(snapshot));
            using var output = File.Create(args[1]);
            encoder.Save(output);
        }
        window.Close();
        Console.WriteLine($"Verificados {nodes.Count} contornos y la comparativa BFS/DFS en paralelo.");
    }
}
