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
        var simulationView = (Grid)window.FindName("SimulationView")!;
        var registrationView = (Grid)window.FindName("RegistrationView")!;
        var navigation = (System.Windows.Controls.Primitives.Popup)window.FindName("NavigationMenu")!;
        var registrationNavigation = (RadioButton)window.FindName("NavRegistration")!;
        var simulationNavigation = (RadioButton)window.FindName("NavSimulation")!;
        if (simulationView.Visibility != Visibility.Visible || registrationView.Visibility != Visibility.Collapsed)
            throw new Exception("La pantalla inicial no es la simulación.");
        registrationNavigation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (registrationView.Visibility != Visibility.Visible || simulationView.Visibility != Visibility.Collapsed ||
            registrationNavigation.IsChecked != true || navigation.IsOpen)
            throw new Exception("El menú no abrió el registro de manera independiente.");
        if (args.Length == 2)
        {
            SavePreview(layout, Path.ChangeExtension(args[1], ".registro-vacio.png"));
            SavePreview((FrameworkElement)navigation.Child, Path.ChangeExtension(args[1], ".menu.png"), 310, 220);
        }
        var registrationOrigin = (ComboBox)window.FindName("CboRegistrationOrigin")!;
        var registrationDestination = (ComboBox)window.FindName("CboRegistrationDestination")!;
        var client = (TextBox)window.FindName("TxtCliente")!;
        var register = (Button)window.FindName("BtnRegistrarPedido")!;
        var orders = (DataGrid)window.FindName("TablaPedidos")!;
        var orderCount = (TextBlock)window.FindName("TxtPedidosCount")!;
        var simulatedGuide = (TextBlock)window.FindName("TxtTrackingNumber")!;
        var registrationGuide = (TextBlock)window.FindName("TxtRegistrationTrackingNumber")!;
        var initialSimulatedGuide = simulatedGuide.Text;
        client.Text = "Cliente de prueba";
        register.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (orders.Items.Count != 0 || orderCount.Text != "0 pedidos")
            throw new Exception("Se registró un pedido sin destino.");
        registrationOrigin.SelectedItem = registrationOrigin.Items.Cast<HubNode>().First(node => node.Id == "QUE");
        registrationDestination.SelectedItem = registrationDestination.Items.Cast<HubNode>().First(node => node.Id == "SAL");
        var registeredGuide = registrationGuide.Text;
        register.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (orders.Items.Count != 1 || orderCount.Text != "1 pedido" || simulatedGuide.Text != initialSimulatedGuide ||
            ((ComboBox)window.FindName("CboDestination")!).SelectedItem != null || registrationGuide.Text == registeredGuide)
            throw new Exception("El registro no guardó el pedido de forma independiente de la simulación.");
        client.Text = "Borrador de otro cliente";
        simulationNavigation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        registrationNavigation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (client.Text != "Borrador de otro cliente" || registrationDestination.SelectedItem is not HubNode { Id: "SAL" })
            throw new Exception("La navegación perdió los datos del formulario de registro.");
        if (args.Length == 2)
        {
            SavePreview(layout, Path.ChangeExtension(args[1], ".registro.png"));
            SavePreview(layout, Path.ChangeExtension(args[1], ".registro-minimo.png"), 1200, 720);
        }
        var simulateOrder = (Button)window.FindName("BtnSimularPedido")!;
        if (!simulateOrder.IsEnabled) throw new Exception("No se habilitó la simulación del pedido registrado.");
        simulateOrder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (simulationView.Visibility != Visibility.Visible || registrationView.Visibility != Visibility.Collapsed ||
            simulatedGuide.Text != registeredGuide || ((ComboBox)window.FindName("CboDestination")!).SelectedItem is not HubNode { Id: "SAL" })
            throw new Exception("No se cargó el pedido registrado en el menú de simulación.");
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
        var delivered = ((ComboBox)window.FindName("ListPedidos")!).Items.Cast<Pedido>().Single();
        if (delivered.Guia != registeredGuide || delivered.Estado != "Entregado")
            throw new Exception("La simulación no actualizó el pedido de la tabla hash.");
        if (args.Length == 2)
        {
            if (compare.IsChecked != true)
                throw new Exception("La opción de comparativa perdió su selección visual.");
            SavePreview(layout, args[1]);
            SavePreview(layout, Path.ChangeExtension(args[1], ".simulacion-minimo.png"), 1200, 720);
        }
        window.Close();
        Console.WriteLine($"Verificados el menú, el registro independiente, el envío de un pedido, {nodes.Count} contornos y la comparativa BFS/DFS en paralelo.");
    }

    private static void SavePreview(FrameworkElement layout, string path, int width = 1440, int height = 900)
    {
        layout.Measure(new Size(width, height));
        layout.Arrange(new Rect(0, 0, width, height));
        layout.UpdateLayout();
        var readyFrame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => readyFrame.Continue = false));
        Dispatcher.PushFrame(readyFrame);
        layout.UpdateLayout();
        var snapshot = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        snapshot.Render(layout);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(snapshot));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
