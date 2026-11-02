namespace UfgEnvios.Models;

public class Pedido
{
    public string Guia { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string OrigenId { get; set; } = string.Empty;
    public string DestinoId { get; set; } = string.Empty;
    public string Estado { get; set; } = "Registrado";

    public override string ToString() => $"{Guia} · {Cliente} · {Estado}";
}