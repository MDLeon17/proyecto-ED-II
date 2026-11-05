using System.IO;
using System.Text.Json;
using UfgEnvios.Algorithms;
using UfgEnvios.Data;
using UfgEnvios.Models;

internal static class CoreChecks
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception(message);
    }
    private static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < 0.000001, message);
    public static void Run()
    {
        var loaded = LogisticsDataLoader.Load(LogisticsDataLoader.DefaultPath);
        var graph = loaded.Graph;
        Check(graph.Nodes.Count == 24 && loaded.Orders.Cantidad == 30, "Datos mínimos de ejemplo.");
        var edges = graph.AdjacencyList.Values.SelectMany(e => e).ToArray();
        Check(edges.Length == 83 && edges.Count(e => !e.Bidirectional) == 5, "44 relaciones: 39 dobles y 5 dirigidas.");
        var hash = new TablaHash();
        foreach (var key in new[] { "Aa", "BB" }) Check(hash.Insertar(new Pedido { Guia = key }), "Insertar colisiones.");
        Check(hash.Eliminar("BB") && hash.Buscar("Aa") != null && hash.Buscar("BB") == null, "Eliminar en colisión.");
        for (int i = 0; i < 2000; i++) hash.Insertar(new Pedido { Guia = $"GUIA-{i}" });
        for (int i = 0; i < 2000; i++) Check(hash.Buscar($"GUIA-{i}")?.Guia == $"GUIA-{i}", "Rehash conserva guías.");
        Check(!hash.Insertar(new Pedido { Guia = "Aa" }) && !hash.Eliminar("INEXISTENTE"), "Duplicados y ausentes.");
        var table = new TablaDispersion<int, int>();
        for (int i = -500; i < 500; i++) table.Add(i, i * 2);
        Check(table.Count == 1000 && table[-500] == -1000 && table.Remove(0) && !table.ContainsKey(0), "Dispersión con hash negativo y crecimiento.");
        var list = new ListaDinamica<int>();
        for (int i = 0; i < 40; i++) list.Add(i);
        list.Insert(0, 100); list.RemoveAt(1); list.Reverse();
        Check(list.Count == 40 && list[39] == 100 && list[0] == 39, "Lista propia.");
        var queue = new Cola<int>(); queue.Enqueue(1); queue.Enqueue(2);
        Check(queue.Dequeue() == 1 && queue.Dequeue() == 2 && queue.Count == 0, "Cola FIFO.");
        queue.Enqueue(3); Check(queue.Dequeue() == 3, "Reutilizar cola vacía.");

        // Grafo pequeño con óptimos distintos: BFS toma A→D; cada peso tiene otro ganador.
        var small = new LogisticsGraph();
        foreach (var id in new[] { "A", "B", "C", "D", "E" }) small.AddNode(new HubNode { Id = id, Name = id });
        small.AddEdge("A", "D", 20, TransportMode.Ground, false, 20, 1);
        small.AddEdge("A", "B", 1, TransportMode.Ground, false, 10, 4);
        small.AddEdge("B", "D", 1, TransportMode.Ground, false, 10, 4);
        small.AddEdge("A", "C", 5, TransportMode.Ground, false, 1, 3);
        small.AddEdge("C", "D", 5, TransportMode.Ground, false, 1, 3);
        Near(small.RunDijkstra("A", "D").TotalDistanceKm, 2, "Óptimo distancia.");
        Near(small.RunDijkstra("A", "D", RouteWeight.Cost).TotalCost, 2, "Óptimo costo.");
        Near(small.RunDijkstra("A", "D", RouteWeight.Time).TotalEstimatedHours, 1, "Óptimo tiempo.");
        Check(small.RunBFS("A", "D").TotalHops == 1, "BFS minimiza aristas.");
        Check(!small.RunBFS("D", "A").Success && !small.RunDFS("D", "A").Success && !small.RunDijkstra("D", "A").Success, "Respetar un solo sentido.");
        Check(small.AreWeaklyConnected("D", "A") && !small.AreWeaklyConnected("A", "E") && small.ComponentCount == 2, "Union-Find débil e isla.");
        small.AddEdge("D", "E", 0, TransportMode.Ground, false, 0, 0);
        Check(small.ComponentCount == 1 && small.RunDijkstra("A", "E").Success, "Actualizar grupos y aceptar peso cero.");
        foreach (var run in new Func<string, string, TraversalResult>[] { graph.RunBFS, graph.RunDFS, (a,b) => graph.RunDijkstra(a,b) })
        {
            Check(!run("NO", "QUE").Success, "ID inválido.");
            var same = run("QUE", "QUE");
            Check(same.Success && same.Path.Count == 1 && same.TotalHops == 0 && same.TotalCost == 0, "Origen igual al destino.");
        }
        try { small.AddEdge("E", "A", -1, TransportMode.Ground); throw new Exception("Se aceptó peso negativo."); }
        catch (ArgumentException) { _checks++; }

        // Floyd-Warshall usa una matriz independiente para contrastar todos los pares y pesos.
        var nodes = graph.Nodes.Values.OrderBy(n => n.Id).ToArray();
        foreach (var weight in Enum.GetValues<RouteWeight>())
        {
            var oracle = new double[nodes.Length, nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            for (int j = 0; j < nodes.Length; j++) oracle[i,j] = i == j ? 0 : graph.GetEdge(nodes[i].Id, nodes[j].Id)?.Weight(weight) ?? double.PositiveInfinity;
            for (int k = 0; k < nodes.Length; k++)
            for (int i = 0; i < nodes.Length; i++)
            for (int j = 0; j < nodes.Length; j++) oracle[i,j] = Math.Min(oracle[i,j], oracle[i,k] + oracle[k,j]);
            for (int i = 0; i < nodes.Length; i++)
            for (int j = 0; j < nodes.Length; j++)
            {
                var result = graph.RunDijkstra(nodes[i].Id, nodes[j].Id, weight);
                Check(result.Success == double.IsFinite(oracle[i,j]), "Alcanzabilidad Dijkstra vs Floyd.");
                if (!result.Success) continue;
                double total = weight == RouteWeight.Distance ? result.TotalDistanceKm : weight == RouteWeight.Cost ? result.TotalCost : result.TotalEstimatedHours;
                Near(total, Math.Round(oracle[i,j], 2), "Dijkstra vs Floyd-Warshall.");
                ValidatePath(graph, result);
            }
        }
        RunQueries(loaded);
        Check(graph.NameIndex.Search("gén").Single().Id == "GEN" && graph.NameIndex.Search("  GÉN ").Single().Id == "GEN", "Trie ignora tildes, espacios y mayúsculas.");
        Check(graph.NameIndex.Search("ostun").Single().Id == "OST" && graph.NameIndex.Search("cuca").Count == 2, "Trie busca por cualquier palabra del nombre.");
        Check(graph.NameIndex.Search("co").Count == 4 && graph.NameIndex.Search("san", 30).Count == 6, "Trie no repite municipios.");
        var heap = new MinHeap();
        foreach (var n in new[] { 5, 3, 9, 1, 7, 3 }) heap.Push(n, n);
        double last = -1;
        while (heap.Count > 0) { heap.Pop(out var p); Check(p >= last, "MinHeap saca de menor a mayor."); last = p; }
        Check(graph.NameIndex.Search("san", 2).Count == 2 && graph.NameIndex.Search("zzzz").Count == 0, "Límite y prefijo ausente.");
        ValidateImports();
        Console.WriteLine($"Estructuras, importación y algoritmos: {_checks} comprobaciones correctas; Dijkstra contrastado en 1,728 rutas con Floyd-Warshall.");
    }
    private static void ValidatePath(LogisticsGraph graph, TraversalResult result)
    {
        double km = 0, cost = 0, hours = 0;
        var seen = new Conjunto<string>();
        foreach (var node in result.Path) Check(seen.Add(node.Id), "Ruta sin ciclos.");
        for (int i = 1; i < result.Path.Count; i++)
        {
            var edge = graph.GetEdge(result.Path[i-1].Id, result.Path[i].Id);
            Check(edge != null, "Tramo dirigido existente.");
            km += edge!.DistanceKm; cost += edge.Cost; hours += edge.EstimatedHours;
        }
        Near(result.TotalDistanceKm, km, "Distancia acumulada.");
        Near(result.TotalCost, Math.Round(cost, 2), "Costo acumulado.");
        Near(result.TotalEstimatedHours, Math.Round(hours, 2), "Tiempo acumulado.");
    }
    private static void RunQueries(LoadedLogisticsData data)
    {
        using var queries = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "Samples", "consultas.json")));
        int count = 0;
        foreach (var query in queries.RootElement.EnumerateArray())
        {
            string option = query.GetProperty("option").GetString()!;
            string input = query.GetProperty("input").GetString()!;
            if (option == "hash") Check((data.Orders.Buscar(input) != null) == query.GetProperty("expected").GetBoolean(), "Consulta hash.");
            else if (option == "prefix") Check(data.Graph.NameIndex.Search(input).Count == query.GetProperty("expected").GetInt32(), "Consulta prefijo.");
            else
            {
                string to = query.GetProperty("to").GetString()!;
                if (option == "connectivity") Check(data.Graph.AreWeaklyConnected(input, to) == query.GetProperty("expected").GetBoolean(), "Consulta grupos.");
                else
                {
                    var result = option switch
                    {
                        "bfs" => data.Graph.RunBFS(input, to),
                        "dfs" => data.Graph.RunDFS(input, to),
                        "distance" => data.Graph.RunDijkstra(input, to),
                        "cost" => data.Graph.RunDijkstra(input, to, RouteWeight.Cost),
                        _ => data.Graph.RunDijkstra(input, to, RouteWeight.Time)
                    };
                    Check(result.Success == query.GetProperty("expected").GetBoolean(), "Consulta recorrido.");
                    if (result.Success) ValidatePath(data.Graph, result);
                }
            }
            count++;
        }
        Check(count == 40, "Cinco consultas por cada una de las ocho opciones.");
    }
    private static void ValidateImports()
    {
        var json = File.ReadAllText(LogisticsDataLoader.DefaultPath);
        var invalid = new[] { "{", "null", "{}", json.Replace("UFG-DEMO-002", "UFG-DEMO-001") };
        var temp = Path.Combine(Path.GetTempPath(), $"ufg-import-{Guid.NewGuid():N}.json");
        try
        {
            foreach (var text in invalid)
            {
                File.WriteAllText(temp, text);
                try { LogisticsDataLoader.Load(temp); throw new Exception("Se aceptó JSON inválido o guía duplicada."); }
                catch (InvalidDataException) { _checks++; }
            }
            var data = JsonSerializer.Deserialize<LogisticsData>(json, LogisticsDataLoader.JsonOptions)!;
            data.Routes[0].FromId = "NO";
            File.WriteAllText(temp, JsonSerializer.Serialize(data, LogisticsDataLoader.JsonOptions));
            try { LogisticsDataLoader.Load(temp); throw new Exception("Se aceptó municipio inexistente."); }
            catch (InvalidDataException) { _checks++; }
            foreach (var mutation in new Action<LogisticsData>[]
            {
                d => d.Nodes[1].Id = d.Nodes[0].Id,
                d => d.Routes[0].DistanceKm = -1,
                d => d.Orders[0].Destinatario = " ",
                d => d.Nodes[0].RegionX = 775,
                d => { d.Routes[1].FromId = d.Routes[0].FromId; d.Routes[1].ToId = d.Routes[0].ToId; },
                d => d.Orders[0].DestinoId = d.Orders[0].OrigenId
            })
            {
                data = JsonSerializer.Deserialize<LogisticsData>(json, LogisticsDataLoader.JsonOptions)!;
                mutation(data);
                File.WriteAllText(temp, JsonSerializer.Serialize(data, LogisticsDataLoader.JsonOptions));
                try { LogisticsDataLoader.Load(temp); throw new Exception("Se aceptaron datos inconsistentes."); }
                catch (InvalidDataException) { _checks++; }
            }
        }
        finally { File.Delete(temp); }
    }
}
