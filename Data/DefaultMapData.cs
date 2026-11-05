using UfgEnvios.Models;

namespace UfgEnvios.Data;

// Compatibilidad con la verificación de contornos; los datos viven en el archivo JSON.
public static class DefaultMapData
{
    public static HubNode[] GetDefaultHubs() => LogisticsDataLoader.Load(LogisticsDataLoader.DefaultPath).Graph.Nodes.Values.OrderBy(n => n.MapNumber).ToArray();
    public static RouteEdge[] GetDefaultRoutes() => LogisticsDataLoader.Load(LogisticsDataLoader.DefaultPath).Graph.AdjacencyList.Values.SelectMany(e => e).ToArray();
}
