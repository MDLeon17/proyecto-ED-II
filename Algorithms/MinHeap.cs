namespace UfgEnvios.Algorithms;

// Montículo mínimo propio sobre arreglos (cola de prioridad).
// Dijkstra lo usa para sacar siempre el municipio con menor peso acumulado.
// Si dos entradas empatan en peso, sale primero la de menor índice (resultado repetible).
public sealed class MinHeap
{
    private int[] _items = new int[16];
    private double[] _priorities = new double[16];
    public int Count { get; private set; }

    public void Push(int item, double priority)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
            Array.Resize(ref _priorities, _priorities.Length * 2);
        }
        int i = Count++;
        _items[i] = item;
        _priorities[i] = priority;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (!IsBefore(i, parent)) break;
            Swap(i, parent);
            i = parent;
        }
    }

    public int Pop(out double priority)
    {
        if (Count == 0) throw new InvalidOperationException("Montículo vacío.");
        int item = _items[0];
        priority = _priorities[0];
        Count--;
        if (Count > 0)
        {
            _items[0] = _items[Count];
            _priorities[0] = _priorities[Count];
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, best = i;
                if (left < Count && IsBefore(left, best)) best = left;
                if (right < Count && IsBefore(right, best)) best = right;
                if (best == i) break;
                Swap(i, best);
                i = best;
            }
        }
        return item;
    }

    private bool IsBefore(int a, int b) =>
        _priorities[a] < _priorities[b] || (_priorities[a] == _priorities[b] && _items[a] < _items[b]);

    private void Swap(int a, int b)
    {
        (_items[a], _items[b]) = (_items[b], _items[a]);
        (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
    }
}
