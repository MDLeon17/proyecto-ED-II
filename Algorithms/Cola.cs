namespace UfgEnvios.Algorithms;

public sealed class Cola<T>
{
    private sealed class Nodo(T value) { public T Value = value; public Nodo? Next; }
    private Nodo? _first, _last;
    public int Count { get; private set; }
    public void Enqueue(T value)
    {
        var node = new Nodo(value);
        if (_last == null) _first = node; else _last.Next = node;
        _last = node; Count++;
    }
    public T Dequeue()
    {
        if (_first == null) throw new InvalidOperationException("Cola vacía.");
        var value = _first.Value; _first = _first.Next; if (--Count == 0) _last = null; return value;
    }
}
