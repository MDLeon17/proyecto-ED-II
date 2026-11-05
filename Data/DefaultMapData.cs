using UfgEnvios.Models;

namespace UfgEnvios.Data;

// Compatibilidad con la verificación de contornos; los datos viven en el archivo JSON.
public static class DefaultMapData
{
    public static HubNode[] GetDefaultHubs()
    {
        var graph = LogisticsDataLoader.Load(LogisticsDataLoader.DefaultPath).Graph;
        var hubs = new HubNode[graph.Nodes.Count];
        int total = 0;
        foreach (var hub in graph.Nodes.Values)
        {
            int j = total++;
            while (j > 0 && hubs[j - 1].MapNumber > hub.MapNumber) { hubs[j] = hubs[j - 1]; j--; }
            hubs[j] = hub;
        }
        return hubs;
    }

    public static RouteEdge[] GetDefaultRoutes()
    {
        var graph = LogisticsDataLoader.Load(LogisticsDataLoader.DefaultPath).Graph;
        int count = 0;
        foreach (var edges in graph.AdjacencyList.Values) count += edges.Count;
        var routes = new RouteEdge[count];
        int next = 0;
        foreach (var edges in graph.AdjacencyList.Values)
            for (int i = 0; i < edges.Count; i++) routes[next++] = edges[i];
        return routes;
    }
}
