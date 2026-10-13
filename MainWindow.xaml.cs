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

    private readonly LogisticsGraph _graph = new();
    private readonly ObservableCollection<string> _trackingLogs = new();
    private readonly MapSurface _singleMap;
    private readonly MapSurface _bfsMap;
    private readonly MapSurface _dfsMap;
    private BitmapImage? _mapImage;
    private MapRegionIndex? _mapRegions;
    private HubNode? _selectedOrigin;
    private HubNode? _selectedDestination;
    private CancellationTokenSource? _runCts;
    private bool _initialized;
    private int _runGeneration;

    private static readonly Color BfsColor = Color.FromRgb(25, 180, 158);
    private static readonly Color DfsColor = Color.FromRgb(242, 122, 94);
    private static readonly Color OriginColor = Color.FromRgb(42, 117, 181);
    private static readonly Color DestinationColor = Color.FromRgb(160, 96, 187);
    private static readonly Color RoadColor = Color.FromRgb(124, 150, 151);

    public MainWindow()
    {
        InitializeComponent();
        _singleMap = new MapSurface(SingleMapCanvas);
        _bfsMap = new MapSurface(BfsMapCanvas);
        _dfsMap = new MapSurface(DfsMapCanvas);
        ListTrackingLogs.ItemsSource = _trackingLogs;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => { _runGeneration++; _runCts?.Cancel(); };
        SliderSpeed.ValueChanged += SliderSpeed_ValueChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        foreach (var node in DefaultMapData.GetDefaultHubs()) _graph.AddNode(node);
        foreach (var edge in DefaultMapData.GetDefaultRoutes())
            _graph.AddEdge(edge.FromId, edge.ToId, edge.DistanceKm, edge.Mode, bidirectional: false);

        _mapImage = new BitmapImage(new Uri("pack://application:,,,/UfgEnvios;component/imagen%20ejemplo.jpg", UriKind.Absolute));
        _mapRegions = new MapRegionIndex(_mapImage, _graph.Nodes.Values);
        var municipalities = _graph.Nodes.Values.OrderBy(node => node.Name).ToList();
        CboOrigin.ItemsSource = municipalities;
        CboDestination.ItemsSource = municipalities;
        CboOrigin.SelectedItem = _graph.Nodes["QUE"];
        _selectedOrigin = _graph.Nodes["QUE"];
        _initialized = true;
        GenerateTrackingNumber();
        ClearPresentation();
        RenderMaps();
        AddLog("Red preparada: selecciona el destino para simular un envío.");
    }

    private void GenerateTrackingNumber() =>
        TxtTrackingNumber.Text = $"UFG-{Random.Shared.Next(1000, 10000)}-{Random.Shared.Next(1000, 10000)}-GT";

    private void BtnRegenerateTracking_Click(object sender, RoutedEventArgs e) => GenerateTrackingNumber();

    private void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSpeedLabel == null) return;
        TxtSpeedLabel.Text = (int)e.NewValue switch { 1 => "Lenta", 2 => "Normal", 3 => "Rápida", _ => "Inmediata" };
    }

    private int AnimationDelay => (int)SliderSpeed.Value switch { 1 => 1300, 2 => 640, 3 => 300, _ => 75 };

    private void RouteMode_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _runCts != null) return;
        ClearPresentation();
        RenderMaps();
        AddLog(RbCompare.IsChecked == true
            ? "Comparativa preparada: BFS y DFS se mostrarán en dos mapas simultáneos."
            : "Método seleccionado. Traza una ruta para iniciar la simulación.");
    }

    private void CboNodes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        _selectedOrigin = CboOrigin.SelectedItem as HubNode;
        _selectedDestination = CboDestination.SelectedItem as HubNode;
        ClearPresentation();
        RenderMaps();
        if (_selectedOrigin != null && _selectedDestination != null)
            AddLog($"Envío preparado: {_selectedOrigin.Name} → {_selectedDestination.Name}.");
    }

    private void BtnSwapPoints_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrigin == null || _selectedDestination == null) return;
        var previousOrigin = CboOrigin.SelectedItem;
        CboOrigin.SelectedItem = CboDestination.SelectedItem;
        CboDestination.SelectedItem = previousOrigin;
    }

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_runCts != null) return;
        DependencyObject? current = e.OriginalSource as DependencyObject;
        while (current != null)
        {
            if (current is Border { Tag: HubNode node })
            {
                if (_selectedOrigin == null || _selectedDestination != null)
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
        SingleMapHost.Visibility = comparing ? Visibility.Collapsed : Visibility.Visible;
        CompareMapsHost.Visibility = comparing ? Visibility.Visible : Visibility.Collapsed;
        var selectedPair = _selectedOrigin != null && _selectedDestination != null;
        var overlay = _mapRegions?.CreateOverlay(_selectedOrigin, _selectedDestination, selectedPair);
        if (comparing)
        {
            RenderMap(_bfsMap, overlay, selectedPair);
            RenderMap(_dfsMap, overlay, selectedPair);
        }
        else RenderMap(_singleMap, overlay, selectedPair);
    }

    private void RenderMap(MapSurface surface, BitmapSource? overlay, bool selectedPair)
    {
        var canvas = surface.Canvas;
        canvas.Children.Clear();
        surface.Markers.Clear();
        surface.ActiveOutlines.Clear();
        surface.ActiveSegments.Clear();

        var image = new Image { Source = _mapImage, Width = 775, Height = 1062, Opacity = 0.45, IsHitTestVisible = false };
        Panel.SetZIndex(image, 0);
        canvas.Children.Add(image);
        if (overlay != null)
        {
            var tint = new Image { Source = overlay, Width = 775, Height = 780, IsHitTestVisible = false };
            Panel.SetZIndex(tint, 1);
            canvas.Children.Add(tint);
        }

        var drawn = new HashSet<string>();
        foreach (var edges in _graph.AdjacencyList.Values)
        foreach (var edge in edges)
        {
            var key = string.CompareOrdinal(edge.FromId, edge.ToId) < 0
                ? $"{edge.FromId}:{edge.ToId}" : $"{edge.ToId}:{edge.FromId}";
            if (!drawn.Add(key)) continue;
            var from = _graph.Nodes[edge.FromId];
            var to = _graph.Nodes[edge.ToId];
            var outline = CreateRoad(from, to, Colors.White, 8);
            var road = CreateRoad(from, to, RoadColor, 3.5);
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
            if (isOrigin) marker.Background = new SolidColorBrush(OriginColor);
            else if (isDestination) marker.Background = new SolidColorBrush(DestinationColor);
            marker.Opacity = selectedPair && !isOrigin && !isDestination ? 0.28 : 1;
            marker.ToolTip = $"{node.MapNumber}. {node.Name}" +
                (isOrigin ? " · ORIGEN" : isDestination ? " · DESTINO" : string.Empty);
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
        Background = new SolidColorBrush(Color.FromRgb(28, 99, 106)),
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
        if (string.CompareOrdinal(a.Id, b.Id) > 0) (a, b) = (b, a);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return new Point(a.X, a.Y);
        var bend = Math.Min(16, length * 0.12);
        return new Point((a.X + b.X) / 2 - dy / length * bend,
                         (a.Y + b.Y) / 2 + dx / length * bend);
    }

    private static PathGeometry RouteGeometry(HubNode from, HubNode to, double progress = 1)
    {
        var start = new Point(from.X, from.Y);
        var end = new Point(to.X, to.Y);
        var control = ControlPoint(from, to);
        var t = Math.Clamp(progress, 0, 1);
        var partialControl = new Point(start.X + (control.X - start.X) * t,
                                       start.Y + (control.Y - start.Y) * t);
        var point = new Point(
            (1 - t) * (1 - t) * start.X + 2 * (1 - t) * t * control.X + t * t * end.X,
            (1 - t) * (1 - t) * start.Y + 2 * (1 - t) * t * control.Y + t * t * end.Y);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new QuadraticBezierSegment(partialControl, point, true));
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Path CreateRoad(HubNode from, HubNode to, Color color, double width) => new()
    {
        Data = RouteGeometry(from, to),
        Stroke = new SolidColorBrush(color),
        StrokeThickness = width,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        IsHitTestVisible = false
    };

    private static void PrepareRoute(MapSurface surface, TraversalResult result, Color color)
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

    private async void BtnDispatch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrigin == null || _selectedDestination == null || _selectedOrigin.Id == _selectedDestination.Id)
        {
            MessageBox.Show("Selecciona dos municipios distintos para realizar el envío.",
                            "UFG ENVIOS", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var cts = new CancellationTokenSource();
        _runCts = cts;
        var generation = ++_runGeneration;
        SetControlsEnabled(false);
        ClearPresentation();
        RenderMaps();

        try
        {
            if (RbCompare.IsChecked == true)
            {
                var bfs = _graph.RunBFS(_selectedOrigin.Id, _selectedDestination.Id);
                var dfs = _graph.RunDFS(_selectedOrigin.Id, _selectedDestination.Id);
                var report = _graph.AnalyzeViability(bfs, dfs);
                ShowMetrics(bfs, dfs);
                TxtWinnerBadge.Text = report.WinnerBadge;
                TxtViabilityExplanation.Text = report.DetailedLogisticsAnalysis;
                TxtSystemStatus.Text = "COMPARANDO EN PARALELO";
                PrepareRoute(_bfsMap, bfs, BfsColor);
                PrepareRoute(_dfsMap, dfs, DfsColor);
                AddLog($"BFS y DFS iniciaron al mismo tiempo: {_selectedOrigin.Name} → {_selectedDestination.Name}.");
                await Task.WhenAll(
                    AnimateRouteAsync(_bfsMap, bfs, "BFS", BfsColor, TxtBfsMapStatus, TxtBfsMapDetail, BfsProgress, cts.Token),
                    AnimateRouteAsync(_dfsMap, dfs, "DFS", DfsColor, TxtDfsMapStatus, TxtDfsMapDetail, DfsProgress, cts.Token));
                cts.Token.ThrowIfCancellationRequested();
                TxtSystemStatus.Text = "COMPARATIVA COMPLETADA";
                AddLog($"Comparativa terminada: {report.SummaryHeadline}");
            }
            else
            {
                var usingDfs = RbDFS.IsChecked == true;
                var result = usingDfs
                    ? _graph.RunDFS(_selectedOrigin.Id, _selectedDestination.Id)
                    : _graph.RunBFS(_selectedOrigin.Id, _selectedDestination.Id);
                var color = usingDfs ? DfsColor : BfsColor;
                var name = usingDfs ? "DFS" : "BFS";
                ShowMetrics(usingDfs ? null : result, usingDfs ? result : null);
                TxtWinnerBadge.Text = $"Ruta {name} preparada";
                TxtViabilityExplanation.Text = $"{result.TotalHops} tramos · {result.TotalDistanceKm:N0} km estimados · {result.TotalEstimatedHours:N1} h estimadas. " +
                    (usingDfs ? "DFS profundiza por una rama antes de retroceder." : "BFS garantiza la menor cantidad de tramos.");
                PrepareRoute(_singleMap, result, color);
                AddLog($"{name} inició: {result.SummaryPath}");
                await AnimateRouteAsync(_singleMap, result, name, color, null, null, null, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                TxtSystemStatus.Text = "ENTREGA COMPLETADA";
                AddLog($"Entrega simulada completada en {_selectedDestination.Name}.");
            }
        }
        catch (OperationCanceledException)
        {
            if (generation == _runGeneration)
            {
                TxtSystemStatus.Text = "SIMULACIÓN DETENIDA";
                AddLog("Simulación detenida.");
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

    private async Task AnimateRouteAsync(MapSurface surface, TraversalResult result, string algorithm, Color color,
        TextBlock? status, TextBlock? detail, ProgressBar? progress, CancellationToken token)
    {
        if (!result.Success || result.Path.Count == 0) return;
        var halo = new Ellipse
        {
            Width = 50, Height = 50,
            Fill = new SolidColorBrush(Color.FromArgb(95, color.R, color.G, color.B)),
            IsHitTestVisible = false
        };
        var parcel = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(17),
            Background = new SolidColorBrush(Color.FromRgb(17, 48, 57)),
            BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(3),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "▣", Foreground = Brushes.White, FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Panel.SetZIndex(halo, 9);
        Panel.SetZIndex(parcel, 10);
        surface.Canvas.Children.Add(halo);
        surface.Canvas.Children.Add(parcel);
        halo.BeginAnimation(OpacityProperty, new DoubleAnimation(0.28, 0.8, TimeSpan.FromMilliseconds(580))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });

        try
        {
            var hops = Math.Max(1, result.TotalHops);
            for (var index = 0; index < result.Path.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var current = result.Path[index];
                Place(halo, new Point(current.X, current.Y));
                Place(parcel, new Point(current.X, current.Y));
                if (surface.Markers.TryGetValue(current.Id, out var marker)
                    && current.Id != _selectedOrigin?.Id && current.Id != _selectedDestination?.Id)
                {
                    marker.Background = new SolidColorBrush(color);
                    marker.Opacity = 1;
                }
                if (status != null) status.Text = $"{index}/{result.TotalHops} tramos";
                else TxtSystemStatus.Text = $"{algorithm} · {index}/{result.TotalHops} TRAMOS";
                if (detail != null) detail.Text = current.Name;
                AddLog($"{DateTime.Now:HH:mm:ss} · {algorithm}: paquete en {current.Name}.");
                if (index == result.Path.Count - 1) break;

                var next = result.Path[index + 1];
                var duration = AnimationDelay;
                var frames = duration < 180 ? 6 : 24;
                for (var frame = 1; frame <= frames; frame++)
                {
                    token.ThrowIfCancellationRequested();
                    var linear = frame / (double)frames;
                    var eased = linear * linear * (3 - 2 * linear);
                    var segment = RouteGeometry(current, next, eased);
                    surface.ActiveOutlines[index].Data = segment;
                    surface.ActiveSegments[index].Data = segment;
                    var endpoint = ((QuadraticBezierSegment)segment.Figures[0].Segments[0]).Point2;
                    Place(halo, endpoint);
                    Place(parcel, endpoint);
                    if (progress != null) progress.Value = (index + linear) / hops * 100;
                    await Task.Delay(Math.Max(10, duration / frames), token);
                }
            }
            if (status != null) status.Text = "Entregado";
            if (detail != null) detail.Text = $"Ruta completa · {result.TotalHops} tramos";
            if (progress != null) progress.Value = 100;
        }
        finally
        {
            halo.BeginAnimation(OpacityProperty, null);
            surface.Canvas.Children.Remove(halo);
            surface.Canvas.Children.Remove(parcel);
        }
    }

    private static void Place(FrameworkElement element, Point point)
    {
        Canvas.SetLeft(element, point.X - element.Width / 2);
        Canvas.SetTop(element, point.Y - element.Height / 2);
    }

    private void ShowMetrics(TraversalResult? bfs, TraversalResult? dfs)
    {
        TxtBfsHops.Text = bfs == null ? "— tramos" : $"{bfs.TotalHops} tramos";
        TxtBfsDist.Text = bfs == null ? "— km" : $"{bfs.TotalDistanceKm:N0} km · {bfs.TotalEstimatedHours:N1} h";
        TxtDfsHops.Text = dfs == null ? "— tramos" : $"{dfs.TotalHops} tramos";
        TxtDfsDist.Text = dfs == null ? "— km" : $"{dfs.TotalDistanceKm:N0} km · {dfs.TotalEstimatedHours:N1} h";
    }

    private void ClearPresentation()
    {
        ShowMetrics(null, null);
        TxtWinnerBadge.Text = "Elige municipios y traza una ruta";
        TxtViabilityExplanation.Text = "Aquí aparecerá el análisis de la ruta.";
        TxtSystemStatus.Text = "LISTO PARA DESPACHO";
        TxtBfsMapStatus.Text = TxtDfsMapStatus.Text = "Esperando ruta";
        TxtBfsMapDetail.Text = "Menor número de tramos";
        TxtDfsMapDetail.Text = "Exploración en profundidad";
        BfsProgress.Value = DfsProgress.Value = 0;
        _trackingLogs.Clear();
        TxtEventsCount.Text = "0 eventos";
    }

    private void SetControlsEnabled(bool enabled)
    {
        BtnDispatch.IsEnabled = enabled;
        BtnSwapPoints.IsEnabled = enabled;
        BtnRegenerateTracking.IsEnabled = enabled;
        CboOrigin.IsEnabled = CboDestination.IsEnabled = enabled;
        RbBFS.IsEnabled = RbDFS.IsEnabled = RbCompare.IsEnabled = enabled;
    }

    private void AddLog(string message)
    {
        _trackingLogs.Add(message);
        TxtEventsCount.Text = _trackingLogs.Count == 1 ? "1 evento" : $"{_trackingLogs.Count} eventos";
        ScrollTrackingLogs.ScrollToBottom();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _runGeneration++;
        _runCts?.Cancel();
        _runCts = null;
        SetControlsEnabled(true);
        ClearPresentation();
        RenderMaps();
        AddLog("Recorrido limpiado. Selecciona dos municipios para un nuevo envío.");
    }
}
