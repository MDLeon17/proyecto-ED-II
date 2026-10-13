namespace UfgEnvios.Models;

public class TraversalResult
{
    public string AlgorithmName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<HubNode> Path { get; set; } = new();
    public List<HubNode> VisitedOrder { get; set; } = new();
    public double TotalDistanceKm { get; set; }
    public int TotalHops => Math.Max(0, Path.Count - 1);
    public double TotalEstimatedHours { get; set; }
    public int NodesEvaluatedCount => VisitedOrder.Count;
    public double ExecutionTimeMs { get; set; }
    public string SummaryPath => Path.Count > 0 ? string.Join(" → ", Path.Select(n => n.Name)) : "Sin ruta";
}
