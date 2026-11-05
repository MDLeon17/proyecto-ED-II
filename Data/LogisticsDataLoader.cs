using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UfgEnvios.Algorithms;
using UfgEnvios.Models;

namespace UfgEnvios.Data;

public sealed class LogisticsData
{
    [JsonRequired] public HubNode[] Nodes { get; set; } = [];
    [JsonRequired] public RouteEdge[] Routes { get; set; } = [];
    [JsonRequired] public Pedido[] Orders { get; set; } = [];
}

public sealed record LoadedLogisticsData(LogisticsGraph Graph, TablaHash Orders);

public static class LogisticsDataLoader
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "Samples", "logistica.json");
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static LoadedLogisticsData Load(string path)
    {
        try
        {
            var data = JsonSerializer.Deserialize<LogisticsData>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("El archivo está vacío.");
            if (data.Nodes == null || data.Routes == null || data.Orders == null || data.Nodes.Length == 0)
                throw new InvalidDataException("El archivo necesita nodes, routes y orders; nodes no puede estar vacío.");
            var graph = new LogisticsGraph();
            var numbers = new Conjunto<int>();
            foreach (var node in data.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || node.Id != node.Id.Trim() ||
                    string.IsNullOrWhiteSpace(node.Name) || graph.Nodes.ContainsKey(node.Id) ||
                    node.MapNumber < 1 || !numbers.Add(node.MapNumber) ||
                    !double.IsFinite(node.X) || !double.IsFinite(node.Y) || node.X < 0 || node.X >= 775 || node.Y < 0 || node.Y >= 780 ||
                    node.RegionX < 0 || node.RegionX >= 775 || node.RegionY < 0 || node.RegionY >= 780)
                    throw new InvalidDataException("Municipio inválido, código repetido o coordenadas fuera del mapa.");
                graph.AddNode(node);
            }
            foreach (var edge in data.Routes)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.FromId) || string.IsNullOrWhiteSpace(edge.ToId))
                    throw new InvalidDataException("Ruta incompleta.");
                graph.AddEdge(edge.FromId, edge.ToId, edge.DistanceKm, edge.Mode, edge.Bidirectional, edge.Cost, edge.EstimatedHours);
            }
            var orders = new TablaHash();
            foreach (var order in data.Orders)
            {
                if (order == null || string.IsNullOrWhiteSpace(order.Guia) || order.Guia != order.Guia.Trim() ||
                    string.IsNullOrWhiteSpace(order.Remitente) || string.IsNullOrWhiteSpace(order.Destinatario) ||
                    string.IsNullOrWhiteSpace(order.OrigenId) || string.IsNullOrWhiteSpace(order.DestinoId) ||
                    order.OrigenId == order.DestinoId || !graph.Nodes.ContainsKey(order.OrigenId) || !graph.Nodes.ContainsKey(order.DestinoId))
                    throw new InvalidDataException("Envío inválido: necesita guía, remitente, destinatario y municipios distintos existentes.");
                if (!orders.Insertar(order)) throw new InvalidDataException($"Guía repetida: {order.Guia}");
            }
            return new(graph, orders);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidDataException($"Datos inválidos: {ex.Message}", ex);
        }
    }
}
