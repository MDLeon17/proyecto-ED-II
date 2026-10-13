namespace UfgEnvios.Models;

public class ViabilityReport
{
    public TraversalResult BfsResult { get; set; } = new();
    public TraversalResult DfsResult { get; set; } = new();
    public int HopsDifference => DfsResult.TotalHops - BfsResult.TotalHops;
    public double DistanceDifferenceKm => DfsResult.TotalDistanceKm - BfsResult.TotalDistanceKm;
    public double TimeDifferenceHours => Math.Round(DfsResult.TotalEstimatedHours - BfsResult.TotalEstimatedHours, 1);
    public string WinnerBadge { get; set; } = string.Empty;
    public string SummaryHeadline { get; set; } = string.Empty;
    public string DetailedLogisticsAnalysis { get; set; } = string.Empty;
}
