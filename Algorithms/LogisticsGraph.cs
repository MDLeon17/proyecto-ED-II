using System.Diagnostics;
using UfgEnvios.Models;

namespace UfgEnvios.Algorithms;

// Módulo original de grafos: Fernando Méndez (ver docs/APORTES.md).
public class LogisticsGraph
{
    public TablaDispersion<string, HubNode> Nodes { get; } = new();
    public TablaDispersion<string, ListaDinamica<RouteEdge>> AdjacencyList { get; } = new();
    public Trie NameIndex { get; } = new();
    private UnionFind? _components;
    private readonly TablaDispersion<string, int> _nodeIndexes = new();

    public void AddNode(HubNode node)
    {
        if (Nodes.ContainsKey(node.Id)) return;
        Nodes.Add(node.Id, node);
        _nodeIndexes.Add(node.Id, _nodeIndexes.Count);
        NameIndex.Insert(node);
        _components = null;
        AdjacencyList.Add(node.Id, new ListaDinamica<RouteEdge>());
    }

    public void AddEdge(string fromId, string toId, double distanceKm, TransportMode mode, bool bidirectional = true,
        double? cost = null, double? estimatedHours = null)
    {
        if (!Nodes.ContainsKey(fromId) || !Nodes.ContainsKey(toId))
            throw new ArgumentException("La conexión debe unir municipios existentes.");
        double routeCost = cost ?? Math.Round(distanceKm * 2.5, 2);
        double hours = estimatedHours ?? Math.Round(distanceKm / 35.0 + 0.15, 2);
        if (!double.IsFinite(distanceKm) || distanceKm < 0 || !double.IsFinite(routeCost) || routeCost < 0 ||
            !double.IsFinite(hours) || hours < 0) throw new ArgumentException("Los pesos deben ser finitos y no negativos.");
        if (fromId == toId || GetEdge(fromId, toId) != null || (bidirectional && GetEdge(toId, fromId) != null))
            throw new ArgumentException("La ruta está repetida o une un municipio consigo mismo.");
        AdjacencyList[fromId].Add(new RouteEdge { FromId = fromId, ToId = toId, DistanceKm = distanceKm, Mode = mode,
            Cost = routeCost, EstimatedHours = hours, Bidirectional = bidirectional });
        if (bidirectional)
            AdjacencyList[toId].Add(new RouteEdge { FromId = toId, ToId = fromId, DistanceKm = distanceKm, Mode = mode,
                Cost = routeCost, EstimatedHours = hours, Bidirectional = true });
        _components = null;
    }

    // Cada conexión bidireccional se representa una sola vez; las dirigidas son independientes.
    public IEnumerable<RouteEdge> GetRoutes() => AdjacencyList.Values.SelectMany(edges => edges)
        .Where(edge => !edge.Bidirectional || string.CompareOrdinal(edge.FromId, edge.ToId) < 0);

    public void SetEdgeDirection(string fromId, string toId, bool bidirectional)
    {
        var edge = GetEdge(fromId, toId)
            ?? throw new ArgumentException("La conexión seleccionada ya no existe.");
        if (edge.Bidirectional == bidirectional) return;
        var reverse = GetEdge(toId, fromId);
        if (bidirectional)
        {
            if (reverse != null)
                throw new ArgumentException("Ya existe otra conexión en sentido contrario. Elimínala antes de convertir esta conexión en bidireccional.");
            AdjacencyList[toId].Add(new RouteEdge
            {
                FromId = toId, ToId = fromId, DistanceKm = edge.DistanceKm, Mode = edge.Mode,
                Cost = edge.Cost, EstimatedHours = edge.EstimatedHours, Bidirectional = true
            });
        }
        else if (reverse != null)
            AdjacencyList[toId].Remove(reverse);
        edge.Bidirectional = bidirectional;
        _components = null;
    }

    public bool RemoveEdge(string fromId, string toId)
    {
        var edge = GetEdge(fromId, toId);
        if (edge == null) return false;
        AdjacencyList[fromId].Remove(edge);
        if (edge.Bidirectional && GetEdge(toId, fromId) is RouteEdge reverse)
            AdjacencyList[toId].Remove(reverse);
        _components = null;
        return true;
    }

    public int ComponentCount { get { EnsureComponents(); return _components!.ComponentCount; } }
    public bool AreWeaklyConnected(string a, string b)
    {
        if (!_nodeIndexes.ContainsKey(a) || !_nodeIndexes.ContainsKey(b)) return false;
        EnsureComponents();
        return _components!.Connected(_nodeIndexes[a], _nodeIndexes[b]);
    }
    private void EnsureComponents()
    {
        if (_components != null) return;
        _components = new UnionFind(Nodes.Count);
        foreach (var edges in AdjacencyList.Values)
        foreach (var edge in edges) _components.Union(_nodeIndexes[edge.FromId], _nodeIndexes[edge.ToId]);
    }

    // Dijkstra con MinHeap: O((V + E) log V).
    public TraversalResult RunDijkstra(string startId, string endId, RouteWeight weight = RouteWeight.Distance)
    {
        var timer = Stopwatch.StartNew();
        string criterion = weight switch { RouteWeight.Cost => "costo", RouteWeight.Time => "tiempo", _ => "distancia" };
        var result = new TraversalResult { AlgorithmName = $"Dijkstra · {criterion}" };
        if (!Nodes.ContainsKey(startId) || !Nodes.ContainsKey(endId) || !AreWeaklyConnected(startId, endId)) return result;

        int count = Nodes.Count;
        var ids = new string[count];
        foreach (var id in Nodes.Keys) ids[_nodeIndexes[id]] = id;
        var distances = new double[count];
        var previous = new int[count];
        var settled = new bool[count];
        for (int i = 0; i < count; i++) { distances[i] = double.PositiveInfinity; previous[i] = -1; }

        int start = _nodeIndexes[startId], end = _nodeIndexes[endId];
        distances[start] = 0;
        var heap = new MinHeap();
        heap.Push(start, 0);
        while (heap.Count > 0)
        {
            int current = heap.Pop(out double best);
            if (settled[current]) continue;
            settled[current] = true;
            result.VisitedOrder.Add(Nodes[ids[current]]);
            if (current == end) break;
            foreach (var edge in AdjacencyList[ids[current]])
            {
                int next = _nodeIndexes[edge.ToId];
                double candidate = best + edge.Weight(weight);
                if (settled[next] || candidate >= distances[next]) continue;
                distances[next] = candidate;
                previous[next] = current;
                heap.Push(next, candidate);
            }
        }

        var parents = new TablaDispersion<string, string?>();
        if (settled[end])
            for (int v = end; v != -1; v = previous[v])
                parents[ids[v]] = previous[v] == -1 ? null : ids[previous[v]];
        CompleteResult(result, parents, endId, timer);
        return result;
    }

    public RouteEdge? GetEdge(string fromId, string toId)
    {
        if (!AdjacencyList.TryGetValue(fromId, out var edges)) return null;
        for (int i = 0; i < edges.Count; i++)
            if (edges[i].ToId == toId) return edges[i];
        return null;
    }

    public TraversalResult RunBFS(string startId, string endId)
    {
        var timer = Stopwatch.StartNew();
        var result = new TraversalResult { AlgorithmName = "BFS · búsqueda en anchura" };
        if (!Nodes.ContainsKey(startId) || !Nodes.ContainsKey(endId) || !AreWeaklyConnected(startId, endId)) return result;
        var queue = new Cola<string>();
        var parents = new TablaDispersion<string, string?> { [startId] = null };
        queue.Enqueue(startId);
        result.VisitedOrder.Add(Nodes[startId]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == endId) break;
            foreach (var edge in AdjacencyList[current])
            {
                if (parents.ContainsKey(edge.ToId)) continue;
                parents[edge.ToId] = current;
                queue.Enqueue(edge.ToId);
                result.VisitedOrder.Add(Nodes[edge.ToId]);
            }
        }
        CompleteResult(result, parents, endId, timer);
        return result;
    }

    public TraversalResult RunDFS(string startId, string endId)
    {
        var timer = Stopwatch.StartNew();
        var result = new TraversalResult { AlgorithmName = "DFS · búsqueda en profundidad" };
        if (!Nodes.ContainsKey(startId) || !Nodes.ContainsKey(endId) || !AreWeaklyConnected(startId, endId)) return result;
        var visited = new Conjunto<string>();
        var parents = new TablaDispersion<string, string?> { [startId] = null };
        bool Visit(string id)
        {
            visited.Add(id);
            result.VisitedOrder.Add(Nodes[id]);
            if (id == endId) return true;
            foreach (var edge in AdjacencyList[id])
            {
                if (visited.Contains(edge.ToId)) continue;
                parents[edge.ToId] = id;
                if (Visit(edge.ToId)) return true;
            }
            return false;
        }
        Visit(startId);
        CompleteResult(result, parents, endId, timer);
        return result;
    }

    private void CompleteResult(TraversalResult result, TablaDispersion<string, string?> parents, string endId, Stopwatch timer)
    {
        timer.Stop();
        result.ExecutionTimeMs = timer.Elapsed.TotalMilliseconds;
        if (!parents.ContainsKey(endId)) return;
        var ids = new ListaDinamica<string>();
        for (string? current = endId; current != null; current = parents[current]) ids.Add(current);
        ids.Reverse();
        var path = new ListaDinamica<HubNode>();
        for (int i = 0; i < ids.Count; i++) path.Add(Nodes[ids[i]]);
        result.Path = path;
        result.Success = true;
        for (var i = 0; i < ids.Count - 1; i++)
        {
            var edge = GetEdge(ids[i], ids[i + 1])!;
            result.TotalDistanceKm += edge.DistanceKm;
            result.TotalEstimatedHours += edge.EstimatedHours;
            result.TotalCost += edge.Cost;
        }
        result.TotalEstimatedHours = Math.Round(result.TotalEstimatedHours, 2);
        result.TotalCost = Math.Round(result.TotalCost, 2);
    }

    public ViabilityReport AnalyzeViability(TraversalResult bfs, TraversalResult dfs)
    {
        var report = new ViabilityReport { BfsResult = bfs, DfsResult = dfs };
        if (!bfs.Success || !dfs.Success)
        {
            report.WinnerBadge = "Ruta no disponible";
            report.SummaryHeadline = "No existe una conexión entre los municipios elegidos.";
            report.DetailedLogisticsAnalysis = "Revisa las conexiones del grafo.";
            return report;
        }
        report.WinnerBadge = bfs.TotalHops < dfs.TotalHops ? "BFS usa menos tramos" : "Mismo número de tramos";
        report.SummaryHeadline = $"BFS: {bfs.TotalHops} tramos · DFS: {dfs.TotalHops} tramos.";
        report.DetailedLogisticsAnalysis = $"BFS garantiza la menor cantidad de tramos ({bfs.TotalHops}); DFS encontró {dfs.TotalHops}. " +
            $"Distancias simuladas: {bfs.TotalDistanceKm:N0} km y {dfs.TotalDistanceKm:N0} km. " +
            "BFS minimiza tramos; selecciona Dijkstra para optimizar kilómetros, costo o tiempo.";
        return report;
    }
}
