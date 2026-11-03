namespace UfgEnvios.Models;

// Etapas por las que pasa un pedido: Registrado -> EnTransito -> Entregado.
// SinRuta se usa cuando el algoritmo no encuentra camino entre origen y destino.
public enum EstadoPedido { Registrado, EnTransito, Entregado, SinRuta }

public class Pedido
{
    public string Guia { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string OrigenId { get; set; } = string.Empty;
    public string DestinoId { get; set; } = string.Empty;
    public EstadoPedido Estado { get; set; } = EstadoPedido.Registrado;

    // Texto que se muestra en pantalla para cada estado
    public string EstadoTexto => Estado switch
    {
        EstadoPedido.Registrado => "Registrado",
        EstadoPedido.EnTransito => "En tránsito",
        EstadoPedido.Entregado => "Entregado",
        _ => "Sin ruta"
    };

    public override string ToString() => $"{Guia} · {Cliente} · {EstadoTexto}";
}
