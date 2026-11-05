using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UfgEnvios.Algorithms;
using UfgEnvios.Data;
using UfgEnvios.Models;

namespace UfgEnvios;

public partial class GraphEditorWindow : Window
{
    private sealed record RouteItem(RouteEdge Edge, string Label, string Details);
    private readonly LogisticsGraph _graph;
    private readonly MapRegionIndex _regions;
    private HubNode? _origin;
    private HubNode? _destination;
    private RouteEdge? _editing;
    private bool _refreshing;
    public event EventHandler? GraphChanged;

    public GraphEditorWindow(LogisticsGraph graph, MapRegionIndex regions)
    {
        InitializeComponent();
        _graph = graph;
        _regions = regions;
        RefreshRoutes();
        RenderEditorMap();
    }

    private void RefreshRoutes()
    {
        _refreshing = true;
        try
        {
            ListEdges.ItemsSource = _graph.GetRoutes()
                .OrderBy(edge => _graph.Nodes[edge.FromId].Name).ThenBy(edge => _graph.Nodes[edge.ToId].Name)
                .Select(edge => new RouteItem(edge,
                    $"{_graph.Nodes[edge.FromId].Name} {(edge.Bidirectional ? "↔" : "→")} {_graph.Nodes[edge.ToId].Name}",
                    $"{(edge.Bidirectional ? "Bidireccional" : "Unidireccional")} · {edge.DistanceKm:N2} km"))
                .ToArray();
            ListEdges.SelectedItem = ListEdges.Items.Cast<RouteItem>().FirstOrDefault(item =>
                ReferenceEquals(item.Edge, _editing) || (_editing?.Bidirectional == true &&
                    item.Edge.FromId == _editing.ToId && item.Edge.ToId == _editing.FromId));
            TxtRouteCount.Text = $"CONEXIONES ({ListEdges.Items.Count})";
        }
        finally { _refreshing = false; }
    }

    private void UpdateForm()
    {
        TxtEdgeOrigin.Text = _origin?.Name ?? "Selecciona un municipio en el mapa";
        TxtEdgeDestination.Text = _destination?.Name ?? "Selecciona otro municipio";
        TxtEditorMode.Text = _editing == null ? "NUEVA CONEXIÓN" : "EDITAR CONEXIÓN";
        BtnSaveEdge.Content = _editing == null ? "Agregar conexión" : "Guardar sentido";
        BtnSaveEdge.IsEnabled = _origin != null && _destination != null;
        BtnDeleteEdge.IsEnabled = _editing != null;
        TxtEdgeDistance.IsReadOnly = _editing != null;
        if (_editing != null)
        {
            RbEdgeTwoWay.IsChecked = _editing.Bidirectional;
            RbEdgeOneWay.IsChecked = !_editing.Bidirectional;
            TxtEdgeDistance.Text = _editing.DistanceKm.ToString("G", CultureInfo.CurrentCulture);
            TxtEdgeWeights.Text = $"Q {_editing.Cost:N2} · {_editing.EstimatedHours:N2} h. Al editar el sentido se conservan los pesos.";
        }
        else
            TxtEdgeWeights.Text = "El costo y el tiempo se estiman a partir de la distancia.";
        RenderEditorMap();
    }

    private void SelectRoute(RouteEdge edge)
    {
        _editing = edge;
        _origin = _graph.Nodes[edge.FromId];
        _destination = _graph.Nodes[edge.ToId];
        RefreshRoutes();
        UpdateForm();
        Feedback("Elige el sentido y pulsa Guardar sentido, o elimina la conexión.");
    }

    private void ListEdges_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshing && ListEdges.SelectedItem is RouteItem item) SelectRoute(item.Edge);
    }

    private void EditorMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? current = e.OriginalSource as DependencyObject;
        HubNode? node = null;
        while (current != null && current != EditorMapCanvas)
        {
            if (current is FrameworkElement { Tag: RouteEdge edge })
            {
                SelectRoute(edge);
                e.Handled = true;
                return;
            }
            if (current is FrameworkElement { Tag: HubNode hub }) { node = hub; break; }
            current = VisualTreeHelper.GetParent(current);
        }
        var id = _regions.FindNodeIdAt(e.GetPosition(EditorMapCanvas));
        node ??= id == null ? null : _graph.Nodes[id];
        if (node == null) { Feedback("Haz clic dentro de un municipio o sobre su número.", true); return; }
        SelectNode(node);
        e.Handled = true;
    }

    // Se comparte el mismo flujo para los números y las áreas de los municipios.
    private void SelectNode(HubNode node)
    {
        if (_origin == null || _destination != null)
        {
            _editing = null;
            _destination = null;
            _origin = node;
            RefreshRoutes();
            UpdateForm();
            Feedback("Ahora selecciona el municipio de destino.");
            return;
        }
        if (_origin.Id == node.Id) { Feedback("El origen y el destino deben ser distintos.", true); return; }
        _destination = node;
        var existing = _graph.GetEdge(_origin.Id, node.Id);
        if (existing != null) { SelectRoute(existing); return; }
        UpdateForm();
        Feedback("Elige el sentido, ingresa la distancia y pulsa Agregar conexión.");
    }

    private void NewEdge_Click(object sender, RoutedEventArgs e)
    {
        _editing = null;
        _origin = null;
        _destination = null;
        TxtEdgeDistance.Text = "10";
        RefreshRoutes();
        UpdateForm();
        Feedback("Selecciona el municipio de origen.");
    }

    private void SaveEdge_Click(object sender, RoutedEventArgs e)
    {
        if (_origin == null || _destination == null) return;
        try
        {
            bool editing = _editing != null;
            bool bidirectional = RbEdgeTwoWay.IsChecked == true;
            if (editing)
                _graph.SetEdgeDirection(_origin.Id, _destination.Id, bidirectional);
            else
            {
                // No se aceptan separadores de miles para evitar confundir 1,5 con 15.
                var input = TxtEdgeDistance.Text.Trim().Replace(',', '.');
                if (!double.TryParse(input, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out var km) || !double.IsFinite(km) || km < 0)
                { Feedback("Ingresa una distancia válida, finita y no negativa (por ejemplo, 12.5).", true); return; }
                _graph.AddEdge(_origin.Id, _destination.Id, km, TransportMode.Ground, bidirectional);
            }
            _editing = _graph.GetEdge(_origin.Id, _destination.Id);
            RefreshRoutes();
            UpdateForm();
            GraphChanged?.Invoke(this, EventArgs.Empty);
            Feedback(editing ? "Sentido actualizado. Los recorridos ya usan este cambio." : "Conexión agregada al mapa y a los recorridos.");
        }
        catch (ArgumentException ex) { Feedback(ex.Message, true); }
    }

    private void DeleteEdge_Click(object sender, RoutedEventArgs e)
    {
        if (_editing == null || !_graph.RemoveEdge(_editing.FromId, _editing.ToId)) return;
        NewEdge_Click(sender, e);
        GraphChanged?.Invoke(this, EventArgs.Empty);
        Feedback("Conexión eliminada. Los recorridos ya usan este cambio.");
    }

    private void Feedback(string text, bool error = false)
    {
        TxtEdgeFeedback.Text = text;
        TxtEdgeFeedback.Foreground = new SolidColorBrush(error ? Color.FromRgb(163, 55, 39) : Color.FromRgb(8, 127, 114));
    }

    private void RenderEditorMap()
    {
        EditorMapCanvas.Children.Clear();
        EditorMapCanvas.Children.Add(new Image { Source = _regions.CreateMap(_origin, _destination, false),
            Width = 775, Height = 780, IsHitTestVisible = false });
        foreach (var edge in _graph.GetRoutes())
        {
            var from = _graph.Nodes[edge.FromId];
            var to = _graph.Nodes[edge.ToId];
            bool selected = _editing != null && (ReferenceEquals(edge, _editing) ||
                (edge.Bidirectional && edge.FromId == _editing.ToId && edge.ToId == _editing.FromId));
            var color = selected ? Color.FromRgb(8, 127, 114) : Color.FromRgb(124, 150, 151);
            string tip = $"{from.Name} {(edge.Bidirectional ? "↔" : "→")} {to.Name}\n{edge.DistanceKm:N2} km · Q {edge.Cost:N2} · {edge.EstimatedHours:N2} h\nClic para editar o eliminar";
            var hit = MainWindow.CreateRoad(from, to, Colors.Transparent, 14);
            hit.IsHitTestVisible = true;
            hit.Tag = edge;
            hit.ToolTip = tip;
            hit.Cursor = Cursors.Hand;
            EditorMapCanvas.Children.Add(hit);
            var road = MainWindow.CreateRoad(from, to, color, selected ? 4 : 2);
            EditorMapCanvas.Children.Add(road);
            AddArrow(from, to, edge, color, tip);
            if (edge.Bidirectional) AddArrow(to, from, edge, color, tip);
        }
        foreach (var node in _graph.Nodes.Values)
        {
            var marker = MainWindow.CreateMarker(node);
            if (node.Id == _origin?.Id) marker.Background = new SolidColorBrush(Color.FromRgb(42, 117, 181));
            else if (node.Id == _destination?.Id) marker.Background = new SolidColorBrush(Color.FromRgb(160, 96, 187));
            marker.ToolTip = $"{node.MapNumber}. {node.Name}";
            Canvas.SetLeft(marker, node.X - 15);
            Canvas.SetTop(marker, node.Y - 15);
            Panel.SetZIndex(marker, 7);
            EditorMapCanvas.Children.Add(marker);
        }
    }

    private void AddArrow(HubNode from, HubNode to, RouteEdge edge, Color color, string tip)
    {
        var arrow = MainWindow.CreateDirectionArrow(from, to, color);
        arrow.Tag = edge;
        arrow.ToolTip = tip;
        arrow.Cursor = Cursors.Hand;
        EditorMapCanvas.Children.Add(arrow);
    }
}
