using UfgEnvios.Models;
namespace UfgEnvios.Algorithms;

public class TablaHash
{
    private class Nodo
    {
        public string Clave;
        public Pedido Valor;
        public Nodo? Siguiente;
        public Nodo(string clave, Pedido valor)
        {
            Clave = clave;
            Valor = valor;
        }
    }

    private Nodo?[] _cubetas = new Nodo?[16];
    public int Cantidad { get; private set; }

    private int Hash(string clave)
    {
        int resultado = 0;
        foreach (char letra in clave)
            resultado = (resultado * 31 + letra) % _cubetas.Length;
        return resultado;
    }

    public bool Insertar(Pedido pedido)
    {
        if (Buscar(pedido.Guia) != null) return false; // LA guia repetida

        if (Cantidad >= _cubetas.Length * 3 / 4) Redimensionar();

        int posicion = Hash(pedido.Guia);
        var nuevo = new Nodo(pedido.Guia, pedido);
        nuevo.Siguiente = _cubetas[posicion]; // colisión: se encadena con el inicio
        _cubetas[posicion] = nuevo;
        Cantidad++;
        return true;
    }

    public Pedido? Buscar(string guia)
    {
        var actual = _cubetas[Hash(guia)];
        while (actual != null)
        {
            if (actual.Clave == guia) return actual.Valor;
            actual = actual.Siguiente;
        }
        return null;
    }

    // Quita el pedido con esa guía de su cadena. Devuelve false si no existía.
    public bool Eliminar(string guia)
    {
        int posicion = Hash(guia);
        Nodo? anterior = null;
        var actual = _cubetas[posicion];
        while (actual != null)
        {
            if (actual.Clave == guia)
            {
                if (anterior == null) _cubetas[posicion] = actual.Siguiente; // era el primero de la cadena
                else anterior.Siguiente = actual.Siguiente;
                Cantidad--;
                return true;
            }
            anterior = actual;
            actual = actual.Siguiente;
        }
        return false;
    }

    public List<Pedido> ObtenerTodos()
    {
        var lista = new List<Pedido>();
        foreach (var cubeta in _cubetas)
        {
            var actual = cubeta;
            while (actual != null)
            {
                lista.Add(actual.Valor);
                actual = actual.Siguiente;
            }
        }
        return lista;
    }

    private void Redimensionar()
    {
        var anteriores = _cubetas;
        _cubetas = new Nodo?[anteriores.Length * 2];
        foreach (var cubeta in anteriores)
        {
            var actual = cubeta;
            while (actual != null)
            {
                var siguiente = actual.Siguiente;
                int posicion = Hash(actual.Clave);
                actual.Siguiente = _cubetas[posicion];
                _cubetas[posicion] = actual;
                actual = siguiente;
            }
        }
    }
}