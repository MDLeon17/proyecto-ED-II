using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace UfgEnvios.Algorithms;

// Arreglo redimensionable propio. Las interfaces permiten enlazarlo directamente a WPF.
public class ListaDinamica<T> : IList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private T[] _items = new T[8];
    public int Count { get; private set; }
    public bool IsReadOnly => false;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ListaDinamica() { }
    public ListaDinamica(IEnumerable<T> items) { foreach (var item in items) Add(item); }
    public T this[int index]
    {
        get { Validate(index); return _items[index]; }
        set { Validate(index); _items[index] = value; Changed(); }
    }
    private void Validate(int index)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
    }
    private void Changed()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
    public void Add(T item) => Insert(Count, item);
    public void Insert(int index, T item)
    {
        if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        for (int i = Count; i > index; i--) _items[i] = _items[i - 1];
        _items[index] = item;
        Count++;
        Changed();
    }
    public void RemoveAt(int index)
    {
        Validate(index);
        for (int i = index; i < Count - 1; i++) _items[i] = _items[i + 1];
        _items[--Count] = default!;
        Changed();
    }
    public bool Remove(T item) { int i = IndexOf(item); if (i < 0) return false; RemoveAt(i); return true; }
    public int IndexOf(T item)
    {
        for (int i = 0; i < Count; i++) if (EqualityComparer<T>.Default.Equals(_items[i], item)) return i;
        return -1;
    }
    public bool Contains(T item) => IndexOf(item) >= 0;
    public void Clear() { Array.Clear(_items); Count = 0; Changed(); }
    public void CopyTo(T[] array, int arrayIndex) => Array.Copy(_items, 0, array, arrayIndex, Count);
    public void Reverse() { Array.Reverse(_items, 0, Count); Changed(); }
    public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; i++) yield return _items[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
