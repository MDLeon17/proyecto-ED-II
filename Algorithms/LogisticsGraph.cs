using System.Diagnostics;
using UfgEnvios.Models;

namespace UfgEnvios.Algorithms;

public class LogisticsGraph
{
    public Dictionary<string, HubNode> Nodes { get; } = new();
    public Dictionary<string, List<RouteEdge>> AdjacencyList { get; } = new();

    public void AddNode(HubNode node)
    {
        if (Nodes.ContainsKey(node.Id)) return;
        Nodes.Add(node.Id, node);
        AdjacencyList.Add(node.Id, new List<RouteEdge>());
    }

    public void AddEdge(string fromId, string toId, double distanceKm, TransportMode mode, bool bidirectional = true)
    {
        if (!Nodes.ContainsKey(fromId) || !Nodes.ContainsKey(toId))
            throw new ArgumentException("La conexión debe unir municipios existentes.");
        AdjacencyList[fromId].Add(new RouteEdge { FromId = fromId, ToId = toId, DistanceKm = distanceKm, Mode = mode });
        if (bidirectional)
            AdjacencyList[toId].Add(new RouteEdge { FromId = toId, ToId = fromId, DistanceKm = distanceKm, Mode = mode });
    }

    public RouteEdge? GetEdge(string fromId, string toId) =>
        AdjacencyList.TryGetValue(fromId, out var edges) ? edges.FirstOrDefault(e => e.ToId == toId) : null;

    public TraversalResult RunBFS(string startId, string endId)
    {
        var timer = Stopwatch.StartNew();
        var result = new TraversalResult { AlgorithmName = "BFS · búsqueda en anchura" };
        if (!Nodes.ContainsKey(startId) || !Nodes.ContainsKey(endId)) return result;
        var queue = new Queue<string>();
        var parents = new Dictionary<string, string?> { [startId] = null };
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
        if (!Nodes.ContainsKey(startId) || !Nodes.ContainsKey(endId)) return result;
        var visited = new HashSet<string>();
        var parents = new Dictionary<string, string?> { [startId] = null };
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

    private void CompleteResult(TraversalResult result, Dictionary<string, string?> parents, string endId, Stopwatch timer)
    {
        timer.Stop();
        result.ExecutionTimeMs = timer.Elapsed.TotalMilliseconds;
        if (!parents.ContainsKey(endId)) return;
        var ids = new List<string>();
        for (string? current = endId; current != null; current = parents[current]) ids.Add(current);
        ids.Reverse();
        result.Path = ids.Select(id => Nodes[id]).ToList();
        result.Success = true;
        for (var i = 0; i < ids.Count - 1; i++)
        {
            var edge = GetEdge(ids[i], ids[i + 1])!;
            result.TotalDistanceKm += edge.DistanceKm;
            result.TotalEstimatedHours += edge.EstimatedHours;
        }
        result.TotalEstimatedHours = Math.Round(result.TotalEstimatedHours, 1);
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
            "BFS no garantiza la menor distancia en kilómetros; para optimizar pesos se necesitaría Dijkstra.";
        return report;
    }
}
