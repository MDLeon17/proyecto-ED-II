using System.Collections;

namespace UfgEnvios.Algorithms;

// Encadenamiento separado y crecimiento al superar un factor de carga de 0.75.
public sealed class TablaDispersion<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private sealed class Nodo(TKey key, TValue value, Nodo? next)
    {
        public TKey Key = key;
        public TValue Value = value;
        public Nodo? Next = next;
    }
    private Nodo?[] _buckets = new Nodo?[16];
    public int Count { get; private set; }
    private int Index(TKey key) => (int)(Hash(key) % (uint)_buckets.Length);

    // Hash propio: polinomio base 131 para texto, constante multiplicativa para int y char.
    private static uint Hash(TKey key)
    {
        unchecked
        {
            switch (key)
            {
                case string text:
                    uint hash = 5381;
                    foreach (char letter in text) hash = hash * 131 + letter;
                    return hash ^ (hash >> 15);
                case int number:
                    return (uint)number * 2654435761u;
                case char letter:
                    return letter * 2654435761u;
                default:
                    throw new NotSupportedException($"La tabla no tiene función hash para {typeof(TKey).Name}.");
            }
        }
    }
    private Nodo? Find(TKey key)
    {
        for (var node = _buckets[Index(key)]; node != null; node = node.Next)
            if (EqualityComparer<TKey>.Default.Equals(node.Key, key)) return node;
        return null;
    }
    public bool ContainsKey(TKey key) => Find(key) != null;
    public bool TryGetValue(TKey key, out TValue value)
    {
        var node = Find(key); value = node == null ? default! : node.Value; return node != null;
    }
    public TValue this[TKey key]
    {
        get => Find(key) is { } node ? node.Value : throw new KeyNotFoundException($"Clave inexistente: {key}");
        set { var node = Find(key); if (node == null) Add(key, value); else node.Value = value; }
    }
    public void Add(TKey key, TValue value)
    {
        if (ContainsKey(key)) throw new ArgumentException($"Clave repetida: {key}");
        if (Count >= _buckets.Length * 3 / 4)
        {
            var previous = _buckets; _buckets = new Nodo?[previous.Length * 2];
            foreach (var head in previous)
            {
                var node = head;
                while (node != null)
                {
                    var next = node.Next; var index = Index(node.Key);
                    node.Next = _buckets[index]; _buckets[index] = node; node = next;
                }
            }
        }
        int position = Index(key); _buckets[position] = new Nodo(key, value, _buckets[position]); Count++;
    }
    public bool Remove(TKey key)
    {
        int index = Index(key); Nodo? previous = null;
        for (var node = _buckets[index]; node != null; node = node.Next)
        {
            if (EqualityComparer<TKey>.Default.Equals(node.Key, key))
            {
                if (previous == null) _buckets[index] = node.Next; else previous.Next = node.Next;
                Count--; return true;
            }
            previous = node;
        }
        return false;
    }
    public void Clear() { _buckets = new Nodo?[16]; Count = 0; }
    public IEnumerable<TValue> Values { get { foreach (var pair in this) yield return pair.Value; } }
    public IEnumerable<TKey> Keys { get { foreach (var pair in this) yield return pair.Key; } }
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var head in _buckets)
            for (var node = head; node != null; node = node.Next) yield return new(node.Key, node.Value);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Conjunto<T> where T : notnull
{
    private readonly TablaDispersion<T, bool> _values = new();
    public bool Add(T value) { if (_values.ContainsKey(value)) return false; _values.Add(value, true); return true; }
    public bool Contains(T value) => _values.ContainsKey(value);
}
