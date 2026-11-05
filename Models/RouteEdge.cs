namespace UfgEnvios.Models;

public enum TransportMode { Ground }
public enum RouteWeight { Distance, Cost, Time }

public class RouteEdge
{
    [System.Text.Json.Serialization.JsonRequired] public string FromId { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonRequired] public string ToId { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonRequired] public double DistanceKm { get; set; }
    public TransportMode Mode { get; set; } = TransportMode.Ground;
    [System.Text.Json.Serialization.JsonRequired] public double EstimatedHours { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public double Cost { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public bool Bidirectional { get; set; }
    public double Weight(RouteWeight weight) => weight switch
    {
        RouteWeight.Cost => Cost,
        RouteWeight.Time => EstimatedHours,
        _ => DistanceKm
    };
    public override string ToString() => $"{FromId} → {ToId} ({DistanceKm:N0} km estimados)";
}
