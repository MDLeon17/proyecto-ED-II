namespace UfgEnvios.Models;

public enum HubType { AirHub, SortCenter, GroundStation }

public class HubNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int MapNumber { get; set; }
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public HubType Type { get; set; }
    public string Region { get; set; } = string.Empty;
    public string Capacity { get; set; } = string.Empty;
    public override string ToString() => Name;
}
