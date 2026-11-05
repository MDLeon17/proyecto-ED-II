using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UfgEnvios.Data;
using UfgEnvios.Models;

namespace UfgEnvios;

public partial class MainWindow
{
    private readonly Grid _consultasVista = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(24, 18, 24, 18) };
    private readonly RadioButton _navConsultas = new();
    private readonly TextBox _consultaGuia = new() { Height = 39, Text = "UFG-DEMO-001" };
    private readonly TextBox _consultaPrefijo = new() { Height = 39 };
    private readonly ListBox _sugerencias = new() { Height = 96, DisplayMemberPath = "Name", Margin = new Thickness(0, 6, 0, 0) };
    private readonly ComboBox _consultaOrigen = new() { Height = 39, DisplayMemberPath = "Name" };
    private readonly ComboBox _consultaDestino = new() { Height = 39, DisplayMemberPath = "Name" };
    private readonly TextBox _consultaResultado = new()
    {
        IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(0, 8, 8, 0), FontSize = 13, Background = Brushes.White, BorderThickness = new Thickness(0)
    };
    private readonly TextBlock _datosResumen = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };

    private void ConfigurarConsultas()
    {
        Grid.SetRow(_consultasVista, 1);
        ((Grid)SimulationView.Parent).Children.Add(_consultasVista);
        _navConsultas.Content = new TextBlock { Text = "Consultas y datos", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14 };
        _navConsultas.Style = NavSimulation.Style;
        _navConsultas.GroupName = NavSimulation.GroupName;
        _navConsultas.Click += (_, _) => MostrarVista("Consultas");
        ((StackPanel)NavSimulation.Parent).Children.Add(_navConsultas);
        RegisterName("NavConsultas", _navConsultas);
        RegisterName("ConsultasView", _consultasVista);
        _consultasVista.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _consultasVista.RowDefinitions.Add(new RowDefinition());
        _consultasVista.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(370) });
        _consultasVista.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        _consultasVista.ColumnDefinitions.Add(new ColumnDefinition());
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var title = TextoVista("Consultas y datos", 29);
        title.FontWeight = FontWeights.Bold;
        heading.Children.Add(title);
        var subtitle = TextoVista("Busca paquetes y explora las rutas de la red de envíos.", 12);
        subtitle.Foreground = (Brush)FindResource("Muted");
        heading.Children.Add(subtitle);
        var headingRow = new Grid();
        headingRow.ColumnDefinitions.Add(new ColumnDefinition());
        headingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headingRow.Children.Add(heading);
        var load = AddButton(headingRow, "Reemplazar datos desde JSON…", CargarDatos_Click);
        load.ToolTip = "Reemplaza municipios, rutas y envíos; limpia el historial de la sesión.";
        load.VerticalAlignment = VerticalAlignment.Top;
        load.Margin = new Thickness(16, 4, 0, 0);
        Grid.SetColumn(load, 1);
        Grid.SetColumnSpan(headingRow, 3);
        _consultasVista.Children.Add(headingRow);
        var form = new StackPanel();
        _datosResumen.FontSize = 12;
        _datosResumen.Foreground = (Brush)FindResource("Muted");
        form.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(237, 245, 243)), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 0, 12, 0), Child = _datosResumen });
        AddQueryLabel(form, "BUSCAR ENVÍO POR GUÍA");
        form.Children.Add(_consultaGuia);
        var search = AddButton(form, "Buscar guía", BuscarGuia_Click);
        search.Style = (Style)FindResource("PrimaryButton");
        RegisterName("BtnBuscarGuia", search);
        AddQueryLabel(form, "AUTOCOMPLETAR MUNICIPIO");
        _consultaPrefijo.ToolTip = "Escribe un prefijo, por ejemplo: san, co o gén (ignora tildes).";
        form.Children.Add(_consultaPrefijo);
        form.Children.Add(_sugerencias);
        _consultaPrefijo.TextChanged += (_, _) => _sugerencias.ItemsSource = _graph.NameIndex.Search(_consultaPrefijo.Text);
        _sugerencias.SelectionChanged += (_, _) =>
        {
            if (_sugerencias.SelectedItem is HubNode hub) _consultaDestino.SelectedItem = hub;
        };
        AddQueryLabel(form, "ORIGEN");
        form.Children.Add(_consultaOrigen);
        AddQueryLabel(form, "DESTINO");
        form.Children.Add(_consultaDestino);
        var buttons = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 12) };
        AddButton(buttons, "BFS", (_, _) => ConsultarRuta("BFS"));
        AddButton(buttons, "DFS", (_, _) => ConsultarRuta("DFS"));
        AddButton(buttons, "Conectividad", ConsultarConectividad_Click);
        AddButton(buttons, "Dijkstra: km", (_, _) => ConsultarRuta("Dijkstra", RouteWeight.Distance));
        RegisterName("BtnConsultarCosto", AddButton(buttons, "Dijkstra: costo", (_, _) => ConsultarRuta("Dijkstra", RouteWeight.Cost)));
        AddButton(buttons, "Dijkstra: tiempo", (_, _) => ConsultarRuta("Dijkstra", RouteWeight.Time));
        var formCard = TarjetaVista(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetRow(formCard, 1);
        _consultasVista.Children.Add(formCard);
        var results = new Grid();
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.RowDefinitions.Add(new RowDefinition());
        var resultLabel = TextoVista("RESULTADO DE LA CONSULTA", 12);
        resultLabel.FontWeight = FontWeights.SemiBold;
        resultLabel.Foreground = (Brush)FindResource("Teal");
        results.Children.Add(resultLabel);
        Grid.SetRow(buttons, 1);
        results.Children.Add(buttons);
        Grid.SetRow(_consultaResultado, 2);
        results.Children.Add(_consultaResultado);
        var resultCard = TarjetaVista(results);
        Grid.SetColumn(resultCard, 2);
        Grid.SetRow(resultCard, 1);
        _consultasVista.Children.Add(resultCard);
        RegisterName("ConsultaGuia", _consultaGuia);
        RegisterName("ConsultaPrefijo", _consultaPrefijo);
        RegisterName("ConsultaSugerencias", _sugerencias);
        RegisterName("ConsultaOrigen", _consultaOrigen);
        RegisterName("ConsultaDestino", _consultaDestino);
        RegisterName("ConsultaResultado", _consultaResultado);
        ActualizarConsultas();
        _consultaResultado.Text = "Busca una guía para consultar remitente, destinatario y estado.\n\nEscribe un prefijo para obtener sugerencias del Trie y elige una como destino.\n\nBFS minimiza tramos; DFS explora en profundidad; Dijkstra minimiza el peso elegido.\n\nConectividad muestra el grupo de Union-Find y comprueba por separado la ruta dirigida.\n\nLos datos son simulados. Los envíos y cambios de estado duran mientras la aplicación está abierta.";
    }

    private static void AddQueryLabel(Panel panel, string text)
    {
        var label = TextoVista(text, 11);
        label.FontWeight = FontWeights.SemiBold;
        label.Foreground = new SolidColorBrush(Color.FromRgb(71, 115, 110));
        label.Margin = new Thickness(0, 12, 0, 6);
        panel.Children.Add(label);
    }

    private Button AddButton(Panel panel, string text, RoutedEventHandler handler)
    {
        var button = new Button { Content = text, Style = (Style)FindResource("SecondaryButton"), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 6, 6, 8) };
        button.Click += handler;
        panel.Children.Add(button);
        return button;
    }

    private void ActualizarConsultas()
    {
        var nodes = _graph.Nodes.Values.OrderBy(n => n.Name).ToArray();
        _consultaOrigen.ItemsSource = nodes;
        _consultaDestino.ItemsSource = nodes;
        _consultaOrigen.SelectedItem = nodes.FirstOrDefault(n => n.Id == "QUE") ?? nodes[0];
        _consultaDestino.SelectedItem = nodes.FirstOrDefault(n => n.Id == "SAL") ?? nodes[0];
        _sugerencias.ItemsSource = _graph.NameIndex.Search(_consultaPrefijo.Text);
        ActualizarResumenDatos();
    }

    private void ActualizarResumenDatos()
    {
        int arcs = _graph.AdjacencyList.Values.Sum(e => e.Count);
        int oneWay = _graph.AdjacencyList.Values.SelectMany(e => e).Count(e => !e.Bidirectional);
        _datosResumen.Text = $"{_graph.Nodes.Count} municipios · {_pedidos.Cantidad} envíos\n{arcs} arcos ({oneWay} de un sentido) · {_graph.ComponentCount} grupo(s)";
    }

    private void BuscarGuia_Click(object sender, RoutedEventArgs e)
    {
        var order = _pedidos.Buscar(_consultaGuia.Text.Trim());
        if (order == null) { _consultaResultado.Text = "No existe un envío con esa guía."; return; }
        _consultaResultado.Text = $"Guía: {order.Guia}\nRemitente: {order.Remitente}\nDestinatario: {order.Destinatario}\nOrigen: {_graph.Nodes[order.OrigenId].Name}\nDestino: {_graph.Nodes[order.DestinoId].Name}\nEstado: {order.EstadoTexto}\n\nEl paquete quedó seleccionado en Envío para su simulación.";
        ListPedidos.SelectedItem = order;
        TablaPedidos.SelectedValue = order.Guia;
        _consultaOrigen.SelectedItem = _graph.Nodes[order.OrigenId];
        _consultaDestino.SelectedItem = _graph.Nodes[order.DestinoId];
    }

    private void ConsultarRuta(string algorithm, RouteWeight weight = RouteWeight.Distance)
    {
        if (_consultaOrigen.SelectedItem is not HubNode from || _consultaDestino.SelectedItem is not HubNode to) return;
        var result = algorithm switch
        {
            "BFS" => _graph.RunBFS(from.Id, to.Id),
            "DFS" => _graph.RunDFS(from.Id, to.Id),
            _ => _graph.RunDijkstra(from.Id, to.Id, weight)
        };
        if (!result.Success) { _consultaResultado.Text = $"{algorithm}: no existe ruta dirigida de {from.Name} a {to.Name}."; return; }
        _consultaResultado.Text = $"{result.AlgorithmName}\n\n{result.SummaryPath}\n\n{result.TotalHops} tramos · {result.TotalDistanceKm:N2} km · Q {result.TotalCost:N2} · {result.TotalEstimatedHours:N2} h\n{result.NodesEvaluatedCount} nodos evaluados · {result.ExecutionTimeMs:N3} ms\n\nOrden de exploración:\n{string.Join(" → ", result.VisitedOrder.Select(n => n.Name))}\n\nTramos (pesos simulados):\n";
        for (int i = 1; i < result.Path.Count; i++)
        {
            var edge = _graph.GetEdge(result.Path[i - 1].Id, result.Path[i].Id)!;
            _consultaResultado.AppendText($"{result.Path[i - 1].Name} {(edge.Bidirectional ? "↔" : "→")} {result.Path[i].Name}: {edge.DistanceKm:N2} km · Q {edge.Cost:N2} · {edge.EstimatedHours:N2} h\n");
        }
    }

    private void ConsultarConectividad_Click(object sender, RoutedEventArgs e)
    {
        if (_consultaOrigen.SelectedItem is not HubNode from || _consultaDestino.SelectedItem is not HubNode to) return;
        bool same = _graph.AreWeaklyConnected(from.Id, to.Id);
        bool reachable = _graph.RunBFS(from.Id, to.Id).Success;
        _consultaResultado.Text = $"Union-Find · {_graph.ComponentCount} grupo(s)\n\n{from.Name} y {to.Name}: {(same ? "pertenecen al mismo grupo" : "pertenecen a grupos distintos")}.\n\nRuta respetando sentidos {from.Name} → {to.Name}: {(reachable ? "disponible" : "no disponible")}.\n\nUnion-Find agrupa ignorando la dirección de las aristas (conectividad débil). La disponibilidad dirigida se comprueba con BFS.\n\nMiembros del grupo de {from.Name}:\n{string.Join("\n", _graph.Nodes.Values.Where(n => _graph.AreWeaklyConnected(from.Id, n.Id)).Select(n => n.Name))}";
    }

    private void CargarDatos_Click(object sender, RoutedEventArgs e)
    {
        if (_runCts != null) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Datos de logística (*.json)|*.json", Title = "Reemplazar municipios, rutas y envíos" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var loaded = LogisticsDataLoader.Load(dialog.FileName);
            var regions = new MapRegionIndex(_mapImage!, loaded.Graph.Nodes.Values);
            // Todo se valida antes de reemplazar la sesión actual.
            _graph = loaded.Graph;
            _pedidos = loaded.Orders;
            _mapRegions = regions;
            _pedidoActual = null;
            _historialEnvios.Clear();
            var nodes = _graph.Nodes.Values.OrderBy(n => n.Name).ToArray();
            CboOrigin.ItemsSource = nodes;
            CboDestination.ItemsSource = nodes;
            CboRegistrationOrigin.ItemsSource = nodes;
            CboRegistrationDestination.ItemsSource = nodes;
            RefrescarPedidos();
            GenerateRegistrationTrackingNumber();
            ActualizarConsultas();
            ClearPresentation();
            RenderMaps();
            _consultaResultado.Text = $"Datos cargados correctamente desde {Path.GetFileName(dialog.FileName)}.\n\nSe reemplazaron los municipios, las rutas y los envíos de la sesión.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _consultaResultado.Text = $"No se cargó el archivo. Los datos actuales se conservan.\n\n{ex.Message}";
        }
    }
}
