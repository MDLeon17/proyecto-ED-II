using System.Text;
using UfgEnvios.Algorithms;
namespace UfgEnvios.Models;

public class TraversalResult
{
    public string AlgorithmName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public ListaDinamica<HubNode> Path { get; set; } = new();
    public ListaDinamica<HubNode> VisitedOrder { get; set; } = new();
    public double TotalDistanceKm { get; set; }
    public int TotalHops => Math.Max(0, Path.Count - 1);
    public double TotalEstimatedHours { get; set; }
    public double TotalCost { get; set; }
    public int NodesEvaluatedCount => VisitedOrder.Count;
    public double ExecutionTimeMs { get; set; }
    public string SummaryPath
    {
        get
        {
            if (Path.Count == 0) return "Sin ruta";
            var text = new StringBuilder(Path[0].Name);
            for (int i = 1; i < Path.Count; i++) text.Append(" → ").Append(Path[i].Name);
            return text.ToString();
        }
    }
}
