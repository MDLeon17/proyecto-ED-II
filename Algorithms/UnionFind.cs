namespace UfgEnvios.Algorithms;

// Compresión de caminos y unión por tamaño. Representa componentes débiles del grafo.
public sealed class UnionFind
{
    private readonly int[] _parent, _size;
    public int ComponentCount { get; private set; }
    public UnionFind(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        _parent = new int[count]; _size = new int[count]; ComponentCount = count;
        for (int i = 0; i < count; i++) { _parent[i] = i; _size[i] = 1; }
    }
    public int Find(int node)
    {
        if (node < 0 || node >= _parent.Length) throw new ArgumentOutOfRangeException(nameof(node));
        while (node != _parent[node]) { _parent[node] = _parent[_parent[node]]; node = _parent[node]; }
        return node;
    }
    public bool Union(int a, int b)
    {
        a = Find(a); b = Find(b); if (a == b) return false;
        if (_size[a] < _size[b]) (a, b) = (b, a);
        _parent[b] = a; _size[a] += _size[b]; ComponentCount--; return true;
    }
    public bool Connected(int a, int b) => Find(a) == Find(b);
}
