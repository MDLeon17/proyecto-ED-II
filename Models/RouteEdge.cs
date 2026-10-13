namespace UfgEnvios.Models;

public enum TransportMode { Ground }

public class RouteEdge
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public TransportMode Mode { get; set; } = TransportMode.Ground;
    public double EstimatedHours => Math.Round(DistanceKm / 35.0 + 0.15, 1);
    public override string ToString() => $"{FromId} → {ToId} ({DistanceKm:N0} km estimados)";
}
