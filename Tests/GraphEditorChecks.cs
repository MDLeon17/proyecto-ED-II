using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UfgEnvios;
using UfgEnvios.Algorithms;
using UfgEnvios.Data;
using UfgEnvios.Models;

internal static class GraphEditorChecks
{
    public static GraphEditorWindow Run(HubNode[] nodes, MapRegionIndex regions)
    {
        var graph = new LogisticsGraph();
        foreach (var node in nodes) graph.AddNode(node);
        var window = new GraphEditorWindow(graph, regions);
        int changes = 0;
        window.GraphChanged += (_, _) => changes++;
        var canvas = (Canvas)window.FindName("EditorMapCanvas");
        var save = (Button)window.FindName("BtnSaveEdge");
        var delete = (Button)window.FindName("BtnDeleteEdge");
        var list = (ListBox)window.FindName("ListEdges");
        var distance = (TextBox)window.FindName("TxtEdgeDistance");
        var oneWay = (RadioButton)window.FindName("RbEdgeOneWay");
        var twoWay = (RadioButton)window.FindName("RbEdgeTwoWay");
        var origin = nodes.Single(node => node.Id == "QUE");
        var destination = nodes.Single(node => node.Id == "SAL");
        void Click(string id)
        {
            var marker = canvas.Children.OfType<Border>().Single(item => item.Tag is HubNode node && node.Id == id);
            marker.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseDownEvent });
        }
        void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (save.IsEnabled || delete.IsEnabled || list.Items.Count != 0) throw new Exception("El editor no inició vacío.");
        Click(origin.Id);
        Click(origin.Id);
        if (save.IsEnabled) throw new Exception("El editor aceptó origen igual a destino.");
        Click(destination.Id);
        if (!save.IsEnabled) throw new Exception("Dos clics en el mapa no prepararon la conexión.");
        foreach (var invalid in new[] { "NaN", "-1", "Infinity", "", "1,2.3" })
        {
            distance.Text = invalid;
            Press(save);
            if (list.Items.Count != 0 || changes != 0) throw new Exception("El editor aceptó una distancia inválida.");
        }
        distance.Text = "12,5";
        oneWay.IsChecked = true;
        Press(save);
        if (list.Items.Count != 1 || changes != 1 || !graph.RunBFS(origin.Id, destination.Id).Success ||
            graph.RunBFS(destination.Id, origin.Id).Success || graph.GetEdge(origin.Id, destination.Id)?.DistanceKm != 12.5)
            throw new Exception("El editor no creó una conexión unidireccional desde el mapa.");
        Press((Button)window.FindName("BtnNewEdge"));
        var connection = canvas.Children.OfType<System.Windows.Shapes.Path>().Single(path => path.Tag is RouteEdge);
        connection.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Mouse.MouseDownEvent });
        if (!delete.IsEnabled) throw new Exception("Hacer clic en una línea no abre su edición.");
        twoWay.IsChecked = true;
        Press(save);
        if (list.Items.Count != 1 || changes != 2 || !graph.RunDijkstra(destination.Id, origin.Id).Success ||
            graph.GetEdge(destination.Id, origin.Id)?.Cost != 31.25)
            throw new Exception("La edición no habilitó el regreso conservando los pesos.");
        // Elegir primero SAL y después QUE permite conservar SAL → QUE al convertir a un sentido.
        Press((Button)window.FindName("BtnNewEdge"));
        Click(destination.Id);
        Click(origin.Id);
        oneWay.IsChecked = true;
        Press(save);
        if (changes != 3 || graph.GetEdge(origin.Id, destination.Id) != null || !graph.RunDFS(destination.Id, origin.Id).Success)
            throw new Exception("La conversión a un sentido no conservó la orientación elegida en el mapa.");
        Press((Button)window.FindName("BtnNewEdge"));
        list.SelectedIndex = 0;
        if (!delete.IsEnabled || !distance.IsReadOnly) throw new Exception("La lista no permite seleccionar una conexión existente.");
        Press(delete);
        if (list.Items.Count != 0 || changes != 4 || graph.AreWeaklyConnected(origin.Id, destination.Id) || delete.IsEnabled)
            throw new Exception("Eliminar no actualizó el mapa, la lista y la conectividad.");
        Click(origin.Id);
        Click(destination.Id);
        distance.Text = "0";
        twoWay.IsChecked = true;
        Press(save);
        if (changes != 5 || graph.GetEdge(destination.Id, origin.Id)?.DistanceKm != 0)
            throw new Exception("No se pudo crear una conexión bidireccional con peso cero.");
        Press(delete);
        if (graph.GetRoutes().Any() || changes != 6) throw new Exception("Eliminar la bidireccional dejó un arco residual.");
        // Captura final con la red completa del archivo de ejemplo para revisar la ventana a tamaño real.
        window.Close();
        Console.WriteLine("Editor de grafos: clics en el mapa, validación, alta, edición de ambos sentidos, selección y eliminación verificados.");
        return new GraphEditorWindow(LogisticsDataLoader.Load(LogisticsDataLoader.DefaultPath).Graph, regions);
    }
}
