using System.Collections;
using System.Collections.Generic;

internal sealed class _003C_003Ez__ReadOnlyArray<T> : IReadOnlyList<T>, IList<T>
{
    private readonly T[] _items;
    public _003C_003Ez__ReadOnlyArray(T[] items) => _items = items;
    public T this[int index] { get => _items[index]; set => throw new System.NotSupportedException(); }
    public int Count => _items.Length;
    public bool IsReadOnly => true;
    public void Add(T item) => throw new System.NotSupportedException();
    public void Clear() => throw new System.NotSupportedException();
    public bool Contains(T item) => ((IList<T>)_items).Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    public int IndexOf(T item) => ((IList<T>)_items).IndexOf(item);
    public void Insert(int index, T item) => throw new System.NotSupportedException();
    public bool Remove(T item) => throw new System.NotSupportedException();
    public void RemoveAt(int index) => throw new System.NotSupportedException();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

internal sealed class _003C_003Ez__ReadOnlySingleElementList<T> : IReadOnlyList<T>, IList<T>
{
    private readonly T _item;
    public _003C_003Ez__ReadOnlySingleElementList(T item) => _item = item;
    public T this[int index] { get => index == 0 ? _item : throw new System.IndexOutOfRangeException(); set => throw new System.NotSupportedException(); }
    public int Count => 1;
    public bool IsReadOnly => true;
    public void Add(T item) => throw new System.NotSupportedException();
    public void Clear() => throw new System.NotSupportedException();
    public bool Contains(T item) => EqualityComparer<T>.Default.Equals(_item, item);
    public void CopyTo(T[] array, int arrayIndex) => array[arrayIndex] = _item;
    public IEnumerator<T> GetEnumerator() { yield return _item; }
    public int IndexOf(T item) => EqualityComparer<T>.Default.Equals(_item, item) ? 0 : -1;
    public void Insert(int index, T item) => throw new System.NotSupportedException();
    public bool Remove(T item) => throw new System.NotSupportedException();
    public void RemoveAt(int index) => throw new System.NotSupportedException();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
