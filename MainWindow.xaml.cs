using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UfgEnvios.Algorithms;
using UfgEnvios.Data;
using UfgEnvios.Models;

namespace UfgEnvios;

public partial class MainWindow : Window
{
    private sealed class MapSurface(Canvas canvas)
    {
        public Canvas Canvas { get; } = canvas;
        public Dictionary<string, Border> Markers { get; } = new();
        public List<Path> ActiveOutlines { get; } = new();
        public List<Path> ActiveSegments { get; } = new();
    }

    private sealed class RegistroHistorial
    {
        public DateTime Fecha { get; init; }
        public string Guia { get; init; } = string.Empty;
        public string Cliente { get; init; } = string.Empty;
        public string Origen { get; init; } = string.Empty;
        public string Destino { get; init; } = string.Empty;
        public string Metodo { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
    }

    private readonly LogisticsGraph _graph = new();
    private readonly TablaHash _pedidos = new();
    private Pedido? _pedidoActual;

    private readonly ObservableCollection<string> _trackingLogs = new();
    private readonly ObservableCollection<RegistroHistorial> _historialEnvios = new();

    // Mapas originales.
    private readonly MapSurface _singleMap;
    private readonly MapSurface _bfsMap;
    private readonly MapSurface _dfsMap;

    // Mapas de la nueva vista de Envío.
    private readonly MapSurface _envioSingleMap;
    private readonly MapSurface _envioBfsMap;
    private readonly MapSurface _envioDfsMap;

    private BitmapImage? _mapImage;
    private MapRegionIndex? _mapRegions;
    private HubNode? _selectedOrigin;
    private HubNode? _selectedDestination;
    private CancellationTokenSource? _runCts;

    private bool _initialized;
    private bool _refreshingPedidos;
    private int _runGeneration;

    private static readonly Color BfsColor = Color.FromRgb(25, 180, 158);
    private static readonly Color DfsColor = Color.FromRgb(242, 122, 94);
    private static readonly Color OriginColor = Color.FromRgb(42, 117, 181);
    private static readonly Color DestinationColor = Color.FromRgb(160, 96, 187);
    private static readonly Color RoadColor = Color.FromRgb(124, 150, 151);

    // Las vistas originales se conservan. Estas son vistas adicionales.
    private readonly Grid _envioVista = new()
    {
        Visibility = Visibility.Collapsed
    };

    private readonly Grid _historialVista = new()
    {
        Visibility = Visibility.Collapsed
    };

    private readonly Canvas _envioCanvas = CrearLienzoEnvio();
    private readonly Canvas _envioBfsCanvas = CrearLienzoEnvio();
    private readonly Canvas _envioDfsCanvas = CrearLienzoEnvio();

    private readonly ComboBox _envioPaquetes = new();
    private readonly RadioButton _navHistorial = new();

    private readonly System.Windows.Controls.Primitives.ToggleButton
        _envioOpciones = new();

    private readonly System.Windows.Controls.Primitives.Popup
        _envioOpcionesPopup = new();

    public MainWindow()
    {
        InitializeComponent();

        _singleMap = new MapSurface(SingleMapCanvas);
        _bfsMap = new MapSurface(BfsMapCanvas);
        _dfsMap = new MapSurface(DfsMapCanvas);

        _envioSingleMap = new MapSurface(_envioCanvas);
        _envioBfsMap = new MapSurface(_envioBfsCanvas);
        _envioDfsMap = new MapSurface(_envioDfsCanvas);

        ListTrackingLogs.ItemsSource = _trackingLogs;

        Loaded += MainWindow_Loaded;

        Closed += (_, _) =>
        {
            _runGeneration++;
            _runCts?.Cancel();
        };

        SliderSpeed.ValueChanged += SliderSpeed_ValueChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;

        ConfigurarVistasYMenu();

        foreach (var node in DefaultMapData.GetDefaultHubs())
            _graph.AddNode(node);

        foreach (var edge in DefaultMapData.GetDefaultRoutes())
        {
            _graph.AddEdge(
                edge.FromId,
                edge.ToId,
                edge.DistanceKm,
                edge.Mode,
                bidirectional: false);
        }

        _mapImage = new BitmapImage(
            new Uri(
                "pack://application:,,,/UfgEnvios;component/imagen%20ejemplo.jpg",
                UriKind.Absolute));

        _mapRegions = new MapRegionIndex(
            _mapImage,
            _graph.Nodes.Values);

        var municipalities = _graph.Nodes.Values
            .OrderBy(node => node.Name)
            .ToList();

        CboOrigin.ItemsSource = municipalities;
        CboDestination.ItemsSource = municipalities;
        CboRegistrationOrigin.ItemsSource = municipalities;
        CboRegistrationDestination.ItemsSource = municipalities;

        CboOrigin.SelectedItem = _graph.Nodes["QUE"];
        CboRegistrationOrigin.SelectedItem = _graph.Nodes["QUE"];
        _selectedOrigin = _graph.Nodes["QUE"];

        _initialized = true;

        GenerateTrackingNumber();
        GenerateRegistrationTrackingNumber();
        RefrescarPedidos();
        ClearPresentation();
        RenderMaps();
        SetControlsEnabled(true);

        MostrarVista("Registro");

        AddLog(
            "Registra un paquete y entra a Envío desde el menú hamburguesa.");
    }

    private void ShowSection(bool registration) =>
        MostrarVista(registration ? "Registro" : "Envío");

    private void NavSimulation_Click(object sender, RoutedEventArgs e) =>
        ShowSection(false);

    private void NavRegistration_Click(object sender, RoutedEventArgs e) =>
        ShowSection(true);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (_envioOpcionesPopup.IsOpen)
        {
            _envioOpciones.IsChecked = false;
            _envioOpciones.Focus();
            e.Handled = true;
        }
        else if (NavigationMenu.IsOpen)
        {
            BtnNavigation.IsChecked = false;
            BtnNavigation.Focus();
            e.Handled = true;
        }
    }

    private void ListPedidos_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_initialized || _refreshingPedidos || _runCts != null)
            return;

        _pedidoActual = ListPedidos.SelectedItem is Pedido elegido
            ? _pedidos.Buscar(elegido.Guia)
            : null;

        TxtTrackingNumber.Text =
            _pedidoActual?.Guia ?? "Sin paquete seleccionado";

        CboOrigin.SelectedItem = _pedidoActual == null
            ? null
            : _graph.Nodes[_pedidoActual.OrigenId];

        CboDestination.SelectedItem = _pedidoActual == null
            ? null
            : _graph.Nodes[_pedidoActual.DestinoId];

        ClearPresentation();
        RenderMaps();
        SetControlsEnabled(true);

        if (_pedidoActual != null)
        {
            AddLog(
                $"Paquete {_pedidoActual.Guia} listo para simular su envío.");
        }
    }

    private void TablaPedidos_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        var haySeleccion =
            _runCts == null && TablaPedidos.SelectedValue is string;

        BtnSimularPedido.IsEnabled = haySeleccion;

        if (FindName("BtnEliminarPedido") is Button eliminar)
            eliminar.IsEnabled = haySeleccion;
    }

    private void BtnEliminarPedido_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_runCts != null ||
            TablaPedidos.SelectedValue is not string guia)
        {
            return;
        }

        var pedido = _pedidos.Buscar(guia);
        if (pedido == null) return;

        var respuesta = MessageBox.Show(
            $"¿Eliminar el pedido {pedido.Guia} de {pedido.Cliente}?",
            "UFG ENVIOS",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (respuesta != MessageBoxResult.Yes) return;

        _pedidos.Eliminar(guia);

        if (_pedidoActual != null && _pedidoActual.Guia == guia)
            _pedidoActual = null;

        RefrescarPedidos();

        ShowRegistrationFeedback(
            $"Pedido {guia} eliminado.",
            true);

        AddLog($"Pedido {guia} eliminado de la tabla hash.");
    }

    private void BtnSimularPedido_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_runCts != null ||
            TablaPedidos.SelectedValue is not string guia)
        {
            return;
        }

        var pedido = _pedidos.Buscar(guia);
        if (pedido == null) return;

        ListPedidos.SelectedItem = null;
        ListPedidos.SelectedItem = pedido;

        ShowSection(false);
    }

    private void CambiarEstadoPedido(EstadoPedido nuevo)
    {
        if (_pedidoActual == null ||
            _selectedOrigin == null ||
            _selectedDestination == null)
        {
            return;
        }

        if (_pedidoActual.OrigenId != _selectedOrigin.Id ||
            _pedidoActual.DestinoId != _selectedDestination.Id)
        {
            return;
        }

        _pedidoActual.Estado = nuevo;

        RefrescarPedidos();

        AddLog(
            $"Pedido {_pedidoActual.Guia}: {_pedidoActual.EstadoTexto}.");
    }

    private string CreateTrackingNumber()
    {
        string guia;

        do
        {
            guia =
                $"UFG-{Random.Shared.Next(1000, 10000)}-" +
                $"{Random.Shared.Next(1000, 10000)}-GT";
        }
        while (_pedidos.Buscar(guia) != null);

        return guia;
    }

    private void GenerateTrackingNumber()
    {
        _pedidoActual = null;
        ListPedidos.SelectedItem = null;
        TxtTrackingNumber.Text = CreateTrackingNumber();
    }

    private void GenerateRegistrationTrackingNumber() =>
        TxtRegistrationTrackingNumber.Text = CreateTrackingNumber();

    private void BtnRegenerateRegistrationTracking_Click(
        object sender,
        RoutedEventArgs e) =>
        GenerateRegistrationTrackingNumber();

    private void BtnRegenerateTracking_Click(
        object sender,
        RoutedEventArgs e) =>
        GenerateTrackingNumber();

    private void SliderSpeed_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSpeedLabel == null) return;

        TxtSpeedLabel.Text = (int)e.NewValue switch
        {
            1 => "Lenta",
            2 => "Normal",
            3 => "Rápida",
            _ => "Inmediata"
        };
    }

    private int AnimationDelay => (int)SliderSpeed.Value switch
    {
        1 => 1300,
        2 => 640,
        3 => 300,
        _ => 75
    };

    private void RouteMode_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (!_initialized || _runCts != null) return;

        _envioOpciones.IsChecked = false;

        ClearPresentation();
        RenderMaps();

        AddLog(RbCompare.IsChecked == true
            ? "Comparativa preparada: BFS y DFS se mostrarán en dos mapas simultáneos."
            : "Método seleccionado. Traza una ruta para iniciar la simulación.");
    }

    private void CboNodes_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_initialized) return;

        _selectedOrigin = CboOrigin.SelectedItem as HubNode;
        _selectedDestination = CboDestination.SelectedItem as HubNode;

        ClearPresentation();
        RenderMaps();

        if (_selectedOrigin != null && _selectedDestination != null)
        {
            AddLog(
                $"Envío preparado: {_selectedOrigin.Name} → {_selectedDestination.Name}.");
        }
    }

    private void BtnSwapPoints_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedOrigin == null || _selectedDestination == null)
            return;

        var previousOrigin = CboOrigin.SelectedItem;

        CboOrigin.SelectedItem = CboDestination.SelectedItem;
        CboDestination.SelectedItem = previousOrigin;
    }

    private void MapCanvas_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_runCts != null) return;

        DependencyObject? current =
            e.OriginalSource as DependencyObject;

        while (current != null)
        {
            if (current is Border { Tag: HubNode node })
            {
                if (_selectedOrigin == null ||
                    _selectedDestination != null)
                {
                    CboDestination.SelectedItem = null;
                    CboOrigin.SelectedItem = node;
                }
                else if (node.Id != _selectedOrigin.Id)
                {
                    CboDestination.SelectedItem = node;
                }

                e.Handled = true;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void RenderMaps()
    {
        if (!_initialized) return;

        var comparing = RbCompare.IsChecked == true;

        SingleMapHost.Visibility =
            comparing ? Visibility.Collapsed : Visibility.Visible;

        CompareMapsHost.Visibility =
            comparing ? Visibility.Visible : Visibility.Collapsed;

        var selectedPair =
            _selectedOrigin != null && _selectedDestination != null;

        var overlay = _mapRegions?.CreateMap(
            _selectedOrigin,
            _selectedDestination,
            selectedPair);

        if (comparing)
        {
            RenderMap(_bfsMap, overlay, selectedPair);
            RenderMap(_dfsMap, overlay, selectedPair);

            RenderMap(_envioBfsMap, overlay, selectedPair);
            RenderMap(_envioDfsMap, overlay, selectedPair);
        }
        else
        {
            RenderMap(_singleMap, overlay, selectedPair);
            RenderMap(_envioSingleMap, overlay, selectedPair);
        }
    }

    private void RenderMap(
        MapSurface surface,
        ImageSource? overlay,
        bool selectedPair)
    {
        var canvas = surface.Canvas;

        canvas.Children.Clear();
        surface.Markers.Clear();
        surface.ActiveOutlines.Clear();
        surface.ActiveSegments.Clear();

        if (overlay != null)
        {
            var tint = new Image
            {
                Source = overlay,
                Width = 775,
                Height = 780,
                IsHitTestVisible = false
            };

            Panel.SetZIndex(tint, 1);
            canvas.Children.Add(tint);
        }

        var drawn = new HashSet<string>();

        foreach (var edges in _graph.AdjacencyList.Values)
        foreach (var edge in edges)
        {
            var key = string.CompareOrdinal(edge.FromId, edge.ToId) < 0
                ? $"{edge.FromId}:{edge.ToId}"
                : $"{edge.ToId}:{edge.FromId}";

            if (!drawn.Add(key)) continue;

            var from = _graph.Nodes[edge.FromId];
            var to = _graph.Nodes[edge.ToId];

            var outline = CreateRoad(from, to, Colors.White, 4);
            var road = CreateRoad(from, to, RoadColor, 1.65);

            outline.Opacity = selectedPair ? 0.55 : 1;
            road.Opacity = selectedPair ? 0.32 : 0.6;

            Panel.SetZIndex(outline, 2);
            Panel.SetZIndex(road, 3);

            canvas.Children.Add(outline);
            canvas.Children.Add(road);
        }

        foreach (var node in _graph.Nodes.Values)
        {
            var marker = CreateMarker(node);

            var isOrigin = node.Id == _selectedOrigin?.Id;
            var isDestination = node.Id == _selectedDestination?.Id;

            if (isOrigin)
                marker.Background = new SolidColorBrush(OriginColor);
            else if (isDestination)
                marker.Background = new SolidColorBrush(DestinationColor);

            marker.Opacity =
                selectedPair && !isOrigin && !isDestination ? 0.28 : 1;

            marker.ToolTip =
                $"{node.MapNumber}. {node.Name}" +
                (isOrigin
                    ? " · ORIGEN"
                    : isDestination ? " · DESTINO" : string.Empty);

            surface.Markers.Add(node.Id, marker);

            Panel.SetZIndex(marker, 7);
            Canvas.SetLeft(marker, node.X - 15);
            Canvas.SetTop(marker, node.Y - 15);

            canvas.Children.Add(marker);
        }
    }

    private static Border CreateMarker(HubNode node) => new()
    {
        Width = 30,
        Height = 30,
        CornerRadius = new CornerRadius(15),
        Background = new SolidColorBrush(
            Color.FromRgb(28, 99, 106)),
        BorderBrush = Brushes.White,
        BorderThickness = new Thickness(2),
        Tag = node,
        Cursor = Cursors.Hand,
        Child = new TextBlock
        {
            Text = node.MapNumber.ToString(),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        }
    };

    private static Point ControlPoint(HubNode a, HubNode b)
    {
        if (string.CompareOrdinal(a.Id, b.Id) > 0)
            (a, b) = (b, a);

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1)
            return new Point(a.X, a.Y);

        var bend = Math.Min(16, length * 0.12);

        return new Point(
            (a.X + b.X) / 2 - dy / length * bend,
            (a.Y + b.Y) / 2 + dx / length * bend);
    }

    private static PathGeometry RouteGeometry(
        HubNode from,
        HubNode to,
        double progress = 1)
    {
        var start = new Point(from.X, from.Y);
        var end = new Point(to.X, to.Y);
        var control = ControlPoint(from, to);
        var t = Math.Clamp(progress, 0, 1);

        var partialControl = new Point(
            start.X + (control.X - start.X) * t,
            start.Y + (control.Y - start.Y) * t);

        var point = new Point(
            (1 - t) * (1 - t) * start.X
                + 2 * (1 - t) * t * control.X
                + t * t * end.X,
            (1 - t) * (1 - t) * start.Y
                + 2 * (1 - t) * t * control.Y
                + t * t * end.Y);

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = start };

        figure.Segments.Add(
            new QuadraticBezierSegment(
                partialControl,
                point,
                true));

        geometry.Figures.Add(figure);

        return geometry;
    }

    private static Path CreateRoad(
        HubNode from,
        HubNode to,
        Color color,
        double width) => new()
    {
        Data = RouteGeometry(from, to),
        Stroke = new SolidColorBrush(color),
        StrokeThickness = width,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        IsHitTestVisible = false
    };

    private static void PrepareRoute(
        MapSurface surface,
        TraversalResult result,
        Color color)
    {
        surface.ActiveOutlines.Clear();
        surface.ActiveSegments.Clear();

        for (var i = 0; i < result.Path.Count - 1; i++)
        {
            var from = result.Path[i];
            var to = result.Path[i + 1];

            var preview = CreateRoad(from, to, color, 5);
            preview.Opacity = 0.18;

            Panel.SetZIndex(preview, 4);
            surface.Canvas.Children.Add(preview);

            var initialGeometry = RouteGeometry(from, to, 0);

            var outline = CreateRoad(from, to, Colors.White, 12);
            outline.Data = initialGeometry;

            Panel.SetZIndex(outline, 5);
            surface.Canvas.Children.Add(outline);
            surface.ActiveOutlines.Add(outline);

            var animated = CreateRoad(from, to, color, 8);
            animated.Data = initialGeometry;

            Panel.SetZIndex(animated, 6);
            surface.Canvas.Children.Add(animated);
            surface.ActiveSegments.Add(animated);
        }
    }

    private async void BtnDispatch_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_runCts != null) return;

        var pedido = _pedidoActual;

        if (pedido == null
            || _selectedOrigin == null
            || _selectedDestination == null
            || _selectedOrigin.Id == _selectedDestination.Id
            || pedido.OrigenId != _selectedOrigin.Id
            || pedido.DestinoId != _selectedDestination.Id)
        {
            MessageBox.Show(
                "Selecciona un paquete registrado con origen y destino distintos.",
                "UFG ENVIOS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var estadoAnterior = pedido.Estado;

        var singleMap = _envioVista.Visibility == Visibility.Visible
            ? _envioSingleMap
            : _singleMap;

        var bfsMap = _envioVista.Visibility == Visibility.Visible
            ? _envioBfsMap
            : _bfsMap;

        var dfsMap = _envioVista.Visibility == Visibility.Visible
            ? _envioDfsMap
            : _dfsMap;

        var cts = new CancellationTokenSource();

        _runCts = cts;
        var generation = ++_runGeneration;

        SetControlsEnabled(false);

        try
        {
            ClearPresentation();
            RenderMaps();

            var paralelo = RbCompare.IsChecked == true;
            var usarDfs = RbDFS.IsChecked == true;

            TraversalResult? bfs = paralelo || !usarDfs
                ? _graph.RunBFS(pedido.OrigenId, pedido.DestinoId)
                : null;

            TraversalResult? dfs = paralelo || usarDfs
                ? _graph.RunDFS(pedido.OrigenId, pedido.DestinoId)
                : null;

            if (bfs?.Success == false || dfs?.Success == false)
            {
                TxtSystemStatus.Text = "SIN RUTA DISPONIBLE";
                CambiarEstadoPedido(EstadoPedido.SinRuta);

                AddLog(
                    $"No existe una ruta para el paquete {pedido.Guia}.");

                return;
            }

            ShowMetrics(bfs, dfs);
            CambiarEstadoPedido(EstadoPedido.EnTransito);

            if (paralelo)
            {
                var report = _graph.AnalyzeViability(bfs!, dfs!);

                TxtWinnerBadge.Text = report.WinnerBadge;
                TxtViabilityExplanation.Text =
                    report.DetailedLogisticsAnalysis;

                TxtSystemStatus.Text = "SIMULANDO EN PARALELO";

                PrepareRoute(bfsMap, bfs!, BfsColor);
                PrepareRoute(dfsMap, dfs!, DfsColor);

                AddLog(
                    $"Paquete {pedido.Guia}: BFS y DFS iniciados en paralelo.");

                await Task.WhenAll(
                    AnimateRouteAsync(
                        bfsMap, bfs!, "BFS", BfsColor,
                        TxtBfsMapStatus, TxtBfsMapDetail,
                        BfsProgress, cts.Token),

                    AnimateRouteAsync(
                        dfsMap, dfs!, "DFS", DfsColor,
                        TxtDfsMapStatus, TxtDfsMapDetail,
                        DfsProgress, cts.Token));
            }
            else
            {
                var result = (usarDfs ? dfs : bfs)!;
                var color = usarDfs ? DfsColor : BfsColor;
                var name = usarDfs ? "DFS" : "BFS";

                TxtWinnerBadge.Text = $"Recorrido {name}";

                TxtViabilityExplanation.Text =
                    $"{result.TotalHops} tramos · " +
                    $"{result.TotalDistanceKm:N0} km · " +
                    $"{result.TotalEstimatedHours:N1} h estimadas.";

                PrepareRoute(singleMap, result, color);

                AddLog($"{name}: {result.SummaryPath}");

                await AnimateRouteAsync(
                    singleMap,
                    result,
                    name,
                    color,
                    null,
                    null,
                    null,
                    cts.Token);
            }

            cts.Token.ThrowIfCancellationRequested();

            if (generation != _runGeneration) return;

            CambiarEstadoPedido(EstadoPedido.Entregado);
            TxtSystemStatus.Text = "ENTREGADO";

            _historialEnvios.Insert(0, new RegistroHistorial
            {
                Fecha = DateTime.Now,
                Guia = pedido.Guia,
                Cliente = pedido.Cliente,
                Origen = _graph.Nodes[pedido.OrigenId].Name,
                Destino = _graph.Nodes[pedido.DestinoId].Name,
                Metodo = paralelo
                    ? "Paralelo (BFS + DFS)"
                    : usarDfs ? "DFS" : "BFS",
                Estado = pedido.EstadoTexto
            });

            AddLog(
                $"Paquete {pedido.Guia} entregado en {_selectedDestination.Name}.");
        }
        catch (OperationCanceledException)
        {
            pedido.Estado = estadoAnterior;

            if (generation == _runGeneration)
            {
                RefrescarPedidos();
                ClearPresentation();
                RenderMaps();

                TxtSystemStatus.Text = "SIMULACIÓN DETENIDA";

                AddLog(
                    $"Simulación cancelada. Estado del paquete: {pedido.EstadoTexto}.");
            }
        }
        catch (Exception ex)
        {
            pedido.Estado = estadoAnterior;

            if (generation == _runGeneration)
            {
                RefrescarPedidos();
                ClearPresentation();
                RenderMaps();

                TxtSystemStatus.Text = "ERROR EN LA SIMULACIÓN";

                AddLog(ex.Message);

                MessageBox.Show(
                    ex.Message,
                    "No se pudo simular el envío",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            if (generation == _runGeneration)
            {
                _runCts = null;
                SetControlsEnabled(true);
            }

            cts.Dispose();
        }
    }

    private async Task AnimateRouteAsync(
        MapSurface surface,
        TraversalResult result,
        string algorithm,
        Color color,
        TextBlock? status,
        TextBlock? detail,
        ProgressBar? progress,
        CancellationToken token)
    {
        if (!result.Success || result.Path.Count == 0)
            return;

        var halo = new Ellipse
        {
            Width = 50,
            Height = 50,
            Fill = new SolidColorBrush(
                Color.FromArgb(95, color.R, color.G, color.B)),
            IsHitTestVisible = false
        };

        var parcel = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = new SolidColorBrush(
                Color.FromRgb(17, 48, 57)),
            BorderBrush = new SolidColorBrush(color),
            BorderThickness = new Thickness(3),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "▣",
                Foreground = Brushes.White,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        Panel.SetZIndex(halo, 9);
        Panel.SetZIndex(parcel, 10);

        surface.Canvas.Children.Add(halo);
        surface.Canvas.Children.Add(parcel);

        halo.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(
                0.28,
                0.8,
                TimeSpan.FromMilliseconds(580))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            });

        try
        {
            var hops = Math.Max(1, result.TotalHops);

            for (var index = 0; index < result.Path.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                var current = result.Path[index];

                Place(halo, new Point(current.X, current.Y));
                Place(parcel, new Point(current.X, current.Y));

                if (surface.Markers.TryGetValue(
                        current.Id,
                        out var marker)
                    && current.Id != _selectedOrigin?.Id
                    && current.Id != _selectedDestination?.Id)
                {
                    marker.Background = new SolidColorBrush(color);
                    marker.Opacity = 1;
                }

                if (status != null)
                    status.Text = $"{index}/{result.TotalHops} tramos";
                else
                    TxtSystemStatus.Text =
                        $"{algorithm} · {index}/{result.TotalHops} TRAMOS";

                if (detail != null)
                    detail.Text = current.Name;

                AddLog(
                    $"{DateTime.Now:HH:mm:ss} · {algorithm}: paquete en {current.Name}.");

                if (index == result.Path.Count - 1)
                    break;

                var next = result.Path[index + 1];
                var duration = AnimationDelay;
                var frames = duration < 180 ? 6 : 24;

                for (var frame = 1; frame <= frames; frame++)
                {
                    token.ThrowIfCancellationRequested();

                    var linear = frame / (double)frames;
                    var eased = linear * linear * (3 - 2 * linear);

                    var segment =
                        RouteGeometry(current, next, eased);

                    surface.ActiveOutlines[index].Data = segment;
                    surface.ActiveSegments[index].Data = segment;

                    var endpoint =
                        ((QuadraticBezierSegment)
                            segment.Figures[0].Segments[0]).Point2;

                    Place(halo, endpoint);
                    Place(parcel, endpoint);

                    if (progress != null)
                        progress.Value = (index + linear) / hops * 100;

                    await Task.Delay(
                        Math.Max(10, duration / frames),
                        token);
                }
            }

            if (status != null)
                status.Text = "Entregado";

            if (detail != null)
                detail.Text = $"Ruta completa · {result.TotalHops} tramos";

            if (progress != null)
                progress.Value = 100;
        }
        finally
        {
            halo.BeginAnimation(OpacityProperty, null);
            surface.Canvas.Children.Remove(halo);
            surface.Canvas.Children.Remove(parcel);
        }
    }

    private static void Place(
        FrameworkElement element,
        Point point)
    {
        Canvas.SetLeft(element, point.X - element.Width / 2);
        Canvas.SetTop(element, point.Y - element.Height / 2);
    }

    private void ShowMetrics(
        TraversalResult? bfs,
        TraversalResult? dfs)
    {
        TxtBfsHops.Text = bfs == null
            ? "— tramos"
            : $"{bfs.TotalHops} tramos";

        TxtBfsDist.Text = bfs == null
            ? "— km"
            : $"{bfs.TotalDistanceKm:N0} km · {bfs.TotalEstimatedHours:N1} h";

        TxtDfsHops.Text = dfs == null
            ? "— tramos"
            : $"{dfs.TotalHops} tramos";

        TxtDfsDist.Text = dfs == null
            ? "— km"
            : $"{dfs.TotalDistanceKm:N0} km · {dfs.TotalEstimatedHours:N1} h";
    }

    private void ClearPresentation()
    {
        ShowMetrics(null, null);

        TxtWinnerBadge.Text =
            "Selecciona un paquete por su número de guía";

        TxtViabilityExplanation.Text =
            "Aquí aparecerá el análisis de la ruta.";

        TxtSystemStatus.Text = _pedidoActual == null
            ? "SELECCIONA UN PAQUETE"
            : "LISTO PARA SIMULAR";

        TxtBfsMapStatus.Text = "Esperando ruta";
        TxtDfsMapStatus.Text = "Esperando ruta";

        TxtBfsMapDetail.Text = "Menor número de tramos";
        TxtDfsMapDetail.Text = "Exploración en profundidad";

        BfsProgress.Value = 0;
        DfsProgress.Value = 0;

        _trackingLogs.Clear();
        TxtEventsCount.Text = "0 eventos";
    }

    private void SetControlsEnabled(bool enabled)
    {
        BtnDispatch.IsEnabled =
            enabled && _pedidoActual != null;

        if (FindName("BtnSwapPoints") is Button intercambio)
            intercambio.IsEnabled = enabled;

        if (FindName("BtnRegenerateTracking") is Button regenerar)
            regenerar.IsEnabled = enabled;

        CboOrigin.IsEnabled = enabled;
        CboDestination.IsEnabled = enabled;

        RbBFS.IsEnabled = enabled;
        RbDFS.IsEnabled = enabled;
        RbCompare.IsEnabled = enabled;

        ListPedidos.IsEnabled = enabled;

        BtnSimularPedido.IsEnabled =
            enabled && TablaPedidos.SelectedValue is string;

        if (FindName("BtnEliminarPedido") is Button eliminar)
        {
            eliminar.IsEnabled =
                enabled && TablaPedidos.SelectedValue is string;
        }

        if (!enabled)
            _envioOpciones.IsChecked = false;
    }

    private void AddLog(string message)
    {
        _trackingLogs.Add(message);

        TxtEventsCount.Text = _trackingLogs.Count == 1
            ? "1 evento"
            : $"{_trackingLogs.Count} eventos";

        ScrollTrackingLogs.ScrollToBottom();
    }

    private void BtnRegistrarPedido_Click(
        object sender,
        RoutedEventArgs e)
    {
        var origin =
            CboRegistrationOrigin.SelectedItem as HubNode;

        var destination =
            CboRegistrationDestination.SelectedItem as HubNode;

        if (origin == null ||
            destination == null ||
            origin.Id == destination.Id)
        {
            ShowRegistrationFeedback(
                "Elige un origen y un destino distintos.",
                false);

            return;
        }

        var cliente = TxtCliente.Text.Trim();

        if (cliente == "")
        {
            ShowRegistrationFeedback(
                "Escribe el nombre del cliente.",
                false);

            TxtCliente.Focus();
            return;
        }

        var pedido = new Pedido
        {
            Guia = TxtRegistrationTrackingNumber.Text,
            Cliente = cliente,
            OrigenId = origin.Id,
            DestinoId = destination.Id
        };

        if (!_pedidos.Insertar(pedido))
        {
            ShowRegistrationFeedback(
                "Esa guía ya está registrada. Genera una nueva con el botón ↻.",
                false);

            return;
        }

        TxtCliente.Clear();

        GenerateRegistrationTrackingNumber();
        RefrescarPedidos();

        TablaPedidos.SelectedValue = pedido.Guia;

        ShowRegistrationFeedback(
            $"Pedido {pedido.Guia} registrado para {pedido.Cliente}.",
            true);

        AddLog(
            $"Pedido {pedido.Guia} registrado para {pedido.Cliente}.");
    }

    private void ShowRegistrationFeedback(
        string message,
        bool success)
    {
        TxtRegistrationFeedback.Text = message;

        TxtRegistrationFeedback.Foreground =
            new SolidColorBrush(
                success
                    ? Color.FromRgb(143, 227, 214)
                    : Color.FromRgb(255, 173, 145));

        TxtRegistrationFeedback.Visibility =
            Visibility.Visible;
    }

    private void RefrescarPedidos()
    {
        var selectedGuide =
            TablaPedidos.SelectedValue as string;

        var pedidos = _pedidos.ObtenerTodos();

        _refreshingPedidos = true;

        try
        {
            ListPedidos.SelectedItem = null;
            ListPedidos.ItemsSource = pedidos;
            ListPedidos.SelectedItem = _pedidoActual;
        }
        finally
        {
            _refreshingPedidos = false;
        }

        TablaPedidos.ItemsSource = pedidos.Select(pedido => new
        {
            pedido.Guia,
            pedido.Cliente,
            Origen = _graph.Nodes[pedido.OrigenId].Name,
            Destino = _graph.Nodes[pedido.DestinoId].Name,
            Estado = pedido.EstadoTexto
        }).ToList();

        TablaPedidos.SelectedValue = selectedGuide;

        int registrados = 0;
        int enTransito = 0;
        int entregados = 0;
        int sinRuta = 0;

        foreach (var registro in pedidos)
        {
            if (registro.Estado == EstadoPedido.Registrado)
                registrados++;
            else if (registro.Estado == EstadoPedido.EnTransito)
                enTransito++;
            else if (registro.Estado == EstadoPedido.Entregado)
                entregados++;
            else
                sinRuta++;
        }

        if (FindName("TxtCountRegistrado") is TextBlock registradosText)
        {
            registradosText.Text = registrados == 1
                ? "1 registrado"
                : $"{registrados} registrados";
        }

        if (FindName("TxtCountTransito") is TextBlock transitoText)
            transitoText.Text = $"{enTransito} en tránsito";

        if (FindName("TxtCountEntregado") is TextBlock entregadosText)
        {
            entregadosText.Text = entregados == 1
                ? "1 entregado"
                : $"{entregados} entregados";
        }

        if (FindName("TxtCountSinRuta") is TextBlock sinRutaText)
            sinRutaText.Text = $"{sinRuta} sin ruta";

        TxtPedidosCount.Text = pedidos.Count == 1
            ? "1 pedido"
            : $"{pedidos.Count} pedidos";

        EmptyPedidosState.Visibility = pedidos.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_pedidoActual == null && _runCts == null)
        {
            CboOrigin.SelectedItem = null;
            CboDestination.SelectedItem = null;
            TxtTrackingNumber.Text = "Sin paquete seleccionado";
        }

        SetControlsEnabled(_runCts == null);
    }

    private void BtnReset_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_runCts != null)
        {
            _runCts.Cancel();
            return;
        }

        ClearPresentation();
        RenderMaps();

        AddLog(
            "Recorrido limpiado. Puedes volver a simular este paquete.");
    }

    // ------------------------------------------------------------
    // Nuevas vistas y ampliación del menú hamburguesa existente.
    // ------------------------------------------------------------

    private static Canvas CrearLienzoEnvio() => new()
    {
        Width = 775,
        Height = 780,
        Background = Brushes.White,
        ClipToBounds = true
    };

    private static void EnlazarVista(
        DependencyObject destino,
        DependencyProperty propiedad,
        object origen,
        string ruta,
        bool dobleVia = false)
    {
        System.Windows.Data.BindingOperations.SetBinding(
            destino,
            propiedad,
            new System.Windows.Data.Binding(ruta)
            {
                Source = origen,
                Mode = dobleVia
                    ? System.Windows.Data.BindingMode.TwoWay
                    : System.Windows.Data.BindingMode.OneWay,
                UpdateSourceTrigger =
                    System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
    }

    private static TextBlock TextoVista(
        string texto,
        double tamano = 13) => new()
    {
        Text = texto,
        FontSize = tamano,
        Foreground = new SolidColorBrush(
            Color.FromRgb(18, 45, 56)),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 7)
    };

    private static TextBlock TextoReflejado(
        object origen,
        string ruta,
        double tamano = 13)
    {
        var texto = TextoVista("", tamano);

        EnlazarVista(
            texto,
            TextBlock.TextProperty,
            origen,
            ruta);

        return texto;
    }

    private static Border TarjetaVista(UIElement contenido) => new()
    {
        Background = Brushes.White,
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16),
        BorderBrush = new SolidColorBrush(
            Color.FromRgb(220, 230, 232)),
        BorderThickness = new Thickness(1),
        Child = contenido
    };

    private void ConfigurarVistasYMenu()
    {
        // Se agregan vistas al contenedor principal.
        // Los controles de las vistas originales permanecen en su lugar.
        var principal = (Grid)SimulationView.Parent;

        Grid.SetRow(_envioVista, 1);
        Grid.SetRow(_historialVista, 1);

        principal.Children.Add(_envioVista);
        principal.Children.Add(_historialVista);

        NavRegistration.Content = TextoVista("Registro", 14);
        NavSimulation.Content = TextoVista("Envío", 14);
        _navHistorial.Content = TextoVista("Historial", 14);

        foreach (var opcion in new[]
        {
            NavRegistration,
            NavSimulation,
            _navHistorial
        })
        {
            if (opcion.Content is TextBlock titulo)
            {
                titulo.Foreground = Brushes.White;
                titulo.FontWeight = FontWeights.SemiBold;
                titulo.Margin = new Thickness(0);
            }
        }

        _navHistorial.Style = NavSimulation.Style;
        _navHistorial.GroupName = NavSimulation.GroupName;
        _navHistorial.Click += (_, _) => MostrarVista("Historial");

        if (NavSimulation.Parent is StackPanel menu)
        {
            menu.Children.Remove(NavRegistration);

            menu.Children.Insert(
                menu.Children.IndexOf(NavSimulation),
                NavRegistration);

            menu.Children.Add(_navHistorial);
        }

        // Vista adicional de Envío.
        _envioVista.Margin = new Thickness(24, 18, 24, 18);

        _envioVista.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        _envioVista.RowDefinitions.Add(new RowDefinition());

        _envioVista.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        var formulario = new Grid
        {
            Margin = new Thickness(0, 0, 0, 16)
        };

        for (var i = 0; i < 4; i++)
            formulario.ColumnDefinitions.Add(new ColumnDefinition());

        _envioPaquetes.Style =
            (Style)FindResource("RouteCombo");

        _envioPaquetes.DisplayMemberPath = "Guia";
        _envioPaquetes.IsTextSearchEnabled = true;

        TextSearch.SetTextPath(_envioPaquetes, "Guia");

        EnlazarVista(
            _envioPaquetes,
            ItemsControl.ItemsSourceProperty,
            ListPedidos,
            "ItemsSource");

        EnlazarVista(
            _envioPaquetes,
            System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            ListPedidos,
            "SelectedItem",
            true);

        EnlazarVista(
            _envioPaquetes,
            IsEnabledProperty,
            ListPedidos,
            "IsEnabled");

        var valores = new FrameworkElement[]
        {
            _envioPaquetes,
            TextoReflejado(CboOrigin, "SelectedItem.Name"),
            TextoReflejado(CboDestination, "SelectedItem.Name"),
            TextoReflejado(ListPedidos, "SelectedItem.EstadoTexto")
        };

        var etiquetas = new[]
        {
            "NÚMERO DE PAQUETE",
            "ORIGEN",
            "DESTINO",
            "ESTADO"
        };

        for (var i = 0; i < valores.Length; i++)
        {
            var campo = new StackPanel();

            campo.Children.Add(TextoVista(etiquetas[i], 11));
            campo.Children.Add(valores[i]);

            var tarjeta = TarjetaVista(campo);

            tarjeta.Margin =
                new Thickness(i == 0 ? 0 : 12, 0, 0, 0);

            Grid.SetColumn(tarjeta, i);
            formulario.Children.Add(tarjeta);
        }

        _envioVista.Children.Add(formulario);

        var cuerpo = new Grid();

        cuerpo.ColumnDefinitions.Add(new ColumnDefinition());

        cuerpo.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(16)
            });

        cuerpo.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(290)
            });

        Grid.SetRow(cuerpo, 1);

        var mapas = new Grid();

        mapas.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        mapas.RowDefinitions.Add(new RowDefinition());

        mapas.Children.Add(
            TextoReflejado(TxtSystemStatus, "Text", 12));

        var individual = new Viewbox
        {
            Child = _envioCanvas,
            Stretch = Stretch.Uniform
        };

        Grid.SetRow(individual, 1);

        EnlazarVista(
            individual,
            VisibilityProperty,
            SingleMapHost,
            "Visibility");

        mapas.Children.Add(individual);

        var paralelo = new Grid();

        paralelo.ColumnDefinitions.Add(new ColumnDefinition());

        paralelo.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(12)
            });

        paralelo.ColumnDefinitions.Add(new ColumnDefinition());

        Grid.SetRow(paralelo, 1);

        EnlazarVista(
            paralelo,
            VisibilityProperty,
            CompareMapsHost,
            "Visibility");

        paralelo.Children.Add(
            CrearMapaParalelo(
                "BFS",
                _envioBfsCanvas,
                TxtBfsMapStatus,
                TxtBfsMapDetail,
                BfsProgress));

        var mapaDfs = CrearMapaParalelo(
            "DFS",
            _envioDfsCanvas,
            TxtDfsMapStatus,
            TxtDfsMapDetail,
            DfsProgress);

        Grid.SetColumn(mapaDfs, 2);

        paralelo.Children.Add(mapaDfs);
        mapas.Children.Add(paralelo);

        cuerpo.Children.Add(TarjetaVista(mapas));

        // Resumen y seguimiento.
        var resumen = new Grid();

        resumen.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        resumen.RowDefinitions.Add(new RowDefinition());

        var metricas = new StackPanel();

        metricas.Children.Add(
            TextoVista("RESUMEN Y SEGUIMIENTO", 14));

        foreach (var origen in new[]
        {
            TxtBfsHops,
            TxtBfsDist,
            TxtDfsHops,
            TxtDfsDist,
            TxtWinnerBadge,
            TxtViabilityExplanation,
            TxtEventsCount
        })
        {
            metricas.Children.Add(
                TextoReflejado(origen, "Text", 11));
        }

        resumen.Children.Add(metricas);

        var eventos = new ItemsControl
        {
            ItemsSource = _trackingLogs
        };

        var fabrica =
            new FrameworkElementFactory(typeof(TextBlock));

        fabrica.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding("."));

        fabrica.SetValue(
            TextBlock.TextWrappingProperty,
            TextWrapping.Wrap);

        fabrica.SetValue(
            TextBlock.FontSizeProperty,
            11.0);

        fabrica.SetValue(
            MarginProperty,
            new Thickness(0, 0, 0, 8));

        eventos.ItemTemplate = new DataTemplate
        {
            VisualTree = fabrica
        };

        var scroll = new ScrollViewer
        {
            Content = eventos,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Grid.SetRow(scroll, 1);
        resumen.Children.Add(scroll);

        var tarjetaResumen = TarjetaVista(resumen);

        Grid.SetColumn(tarjetaResumen, 2);
        cuerpo.Children.Add(tarjetaResumen);

        _envioVista.Children.Add(cuerpo);

        // Acciones inferiores: Simular envío y ⋮.
        var acciones = new DockPanel
        {
            Margin = new Thickness(0, 14, 0, 0),
            LastChildFill = false
        };

        Grid.SetRow(acciones, 2);

        _envioOpciones.Content = "⋮";
        _envioOpciones.FontSize = 26;
        _envioOpciones.Width = 46;
        _envioOpciones.Height = 44;
        _envioOpciones.Margin = new Thickness(8, 0, 0, 0);
        _envioOpciones.ToolTip = "BFS, DFS o paralelo";

        EnlazarVista(
            _envioOpciones,
            IsEnabledProperty,
            RbBFS,
            "IsEnabled");

        DockPanel.SetDock(_envioOpciones, Dock.Right);
        acciones.Children.Add(_envioOpciones);

        var simular = new Button
        {
            Content = "Simular envío",
            Width = 210,
            Style = (Style)FindResource("PrimaryButton")
        };

        EnlazarVista(
            simular,
            IsEnabledProperty,
            BtnDispatch,
            "IsEnabled");

        simular.Click += BtnDispatch_Click;

        DockPanel.SetDock(simular, Dock.Right);
        acciones.Children.Add(simular);

        var limpiar = new Button
        {
            Content = "Limpiar recorrido",
            Width = 160,
            Style = (Style)FindResource("SecondaryButton")
        };

        limpiar.Click += BtnReset_Click;
        acciones.Children.Add(limpiar);

        var opciones = new StackPanel();

        opciones.Children.Add(
            TextoVista("RECORRIDO", 12));

        var nombres = new[]
        {
            "BFS · menos tramos",
            "DFS · profundidad",
            "Paralelo · BFS y DFS"
        };

        var originales = new[]
        {
            RbBFS,
            RbDFS,
            RbCompare
        };

        for (var i = 0; i < originales.Length; i++)
        {
            var radio = new RadioButton
            {
                Content = nombres[i],
                GroupName = "OpcionesEnvio",
                Margin = new Thickness(0, 5, 0, 5)
            };

            EnlazarVista(
                radio,
                System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                originales[i],
                "IsChecked",
                true);

            EnlazarVista(
                radio,
                IsEnabledProperty,
                originales[i],
                "IsEnabled");

            opciones.Children.Add(radio);
        }

        opciones.Children.Add(
            TextoVista("VELOCIDAD", 11));

        var velocidad = new Slider
        {
            Minimum = 1,
            Maximum = 4,
            TickFrequency = 1,
            IsSnapToTickEnabled = true
        };

        EnlazarVista(
            velocidad,
            System.Windows.Controls.Primitives.RangeBase.ValueProperty,
            SliderSpeed,
            "Value",
            true);

        opciones.Children.Add(velocidad);

        opciones.Children.Add(
            TextoReflejado(TxtSpeedLabel, "Text", 11));

        _envioOpcionesPopup.PlacementTarget = _envioOpciones;

        _envioOpcionesPopup.Placement =
            System.Windows.Controls.Primitives.PlacementMode.Top;

        _envioOpcionesPopup.StaysOpen = false;
        _envioOpcionesPopup.AllowsTransparency = true;
        _envioOpcionesPopup.Child = TarjetaVista(opciones);

        ((Border)_envioOpcionesPopup.Child).Width = 285;

        EnlazarVista(
            _envioOpcionesPopup,
            System.Windows.Controls.Primitives.Popup.IsOpenProperty,
            _envioOpciones,
            "IsChecked",
            true);

        acciones.Children.Add(_envioOpcionesPopup);
        _envioVista.Children.Add(acciones);

        // Vista adicional de Historial.
        _historialVista.Margin =
            new Thickness(24, 18, 24, 18);

        _historialVista.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        _historialVista.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        _historialVista.RowDefinitions.Add(new RowDefinition());

        _historialVista.Children.Add(
            TextoVista("Historial de entregas", 26));

        var contador = TextoVista("", 12);

        contador.SetBinding(
            TextBlock.TextProperty,
            new System.Windows.Data.Binding("Count")
            {
                Source = _historialEnvios,
                StringFormat = "Envíos completados: {0}"
            });

        Grid.SetRow(contador, 1);
        _historialVista.Children.Add(contador);

        var tabla = new DataGrid
        {
            ItemsSource = _historialEnvios,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            RowHeight = 42
        };

        var propiedades = new[]
        {
            "Fecha",
            "Guia",
            "Cliente",
            "Origen",
            "Destino",
            "Metodo",
            "Estado"
        };

        var cabeceras = new[]
        {
            "Fecha",
            "Guía",
            "Cliente",
            "Origen",
            "Destino",
            "Recorrido",
            "Estado"
        };

        for (var i = 0; i < propiedades.Length; i++)
        {
            var binding =
                new System.Windows.Data.Binding(propiedades[i]);

            if (i == 0)
                binding.StringFormat = "{0:dd/MM/yyyy HH:mm:ss}";

            tabla.Columns.Add(
                new DataGridTextColumn
                {
                    Header = cabeceras[i],
                    Binding = binding,
                    Width = new DataGridLength(
                        1,
                        DataGridLengthUnitType.Star),
                    MinWidth = i == 0 ? 150 : 95
                });
        }

        var contenedorTabla = new Grid();
        contenedorTabla.Children.Add(tabla);

        var vacio = TextoVista(
            "Todavía no hay entregas simuladas.",
            18);

        vacio.HorizontalAlignment = HorizontalAlignment.Center;
        vacio.VerticalAlignment = VerticalAlignment.Center;
        vacio.IsHitTestVisible = false;

        var estiloVacio = new Style(typeof(TextBlock));

        estiloVacio.Setters.Add(
            new Setter(
                VisibilityProperty,
                Visibility.Collapsed));

        var sinRegistros = new DataTrigger
        {
            Binding = new System.Windows.Data.Binding("Count")
            {
                Source = _historialEnvios
            },
            Value = 0
        };

        sinRegistros.Setters.Add(
            new Setter(
                VisibilityProperty,
                Visibility.Visible));

        estiloVacio.Triggers.Add(sinRegistros);
        vacio.Style = estiloVacio;

        contenedorTabla.Children.Add(vacio);

        Grid.SetRow(contenedorTabla, 2);
        _historialVista.Children.Add(contenedorTabla);
    }

    private static Grid CrearMapaParalelo(
        string nombre,
        Canvas lienzo,
        TextBlock estado,
        TextBlock detalle,
        ProgressBar progreso)
    {
        var panel = new Grid();

        panel.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        panel.RowDefinitions.Add(new RowDefinition());

        panel.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        panel.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(5) });

        var encabezado = new StackPanel();

        encabezado.Children.Add(TextoVista(nombre, 14));
        encabezado.Children.Add(
            TextoReflejado(estado, "Text", 10));

        panel.Children.Add(encabezado);

        var mapa = new Viewbox
        {
            Child = lienzo,
            Stretch = Stretch.Uniform
        };

        Grid.SetRow(mapa, 1);
        panel.Children.Add(mapa);

        var textoDetalle =
            TextoReflejado(detalle, "Text", 10);

        Grid.SetRow(textoDetalle, 2);
        panel.Children.Add(textoDetalle);

        var barra = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100
        };

        EnlazarVista(
            barra,
            System.Windows.Controls.Primitives.RangeBase.ValueProperty,
            progreso,
            "Value");

        Grid.SetRow(barra, 3);
        panel.Children.Add(barra);

        return panel;
    }

    private void MostrarVista(string vista)
    {
        var registro = vista == "Registro";
        var envio = vista == "Envío" || vista == "Envio";

        SimulationView.Visibility = Visibility.Collapsed;

        RegistrationView.Visibility =
            registro ? Visibility.Visible : Visibility.Collapsed;

        _envioVista.Visibility =
            envio ? Visibility.Visible : Visibility.Collapsed;

        _historialVista.Visibility = vista == "Historial"
            ? Visibility.Visible
            : Visibility.Collapsed;

        NavRegistration.IsChecked = registro;
        NavSimulation.IsChecked = envio;
        _navHistorial.IsChecked = vista == "Historial";

        TxtActiveSection.Text = envio ? "Envío" : vista;

        BtnNavigation.IsChecked = false;
        _envioOpciones.IsChecked = false;

        if (registro)
            TxtCliente.Focus();
        else if (envio)
            _envioPaquetes.Focus();
    }
}