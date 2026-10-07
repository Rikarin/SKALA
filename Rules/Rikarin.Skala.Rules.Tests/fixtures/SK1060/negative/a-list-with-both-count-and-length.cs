// ⚠ #425: implicit index support reads `Length` when the type declares one, and this list's `Length` is
// its capacity. `b[b.Count - 1]` is the last element and `b[^1]` is the slot past the last one written:
// measured for #412's audit as `20` before the fix and `0` after it.
using System;
using System.Collections;
using System.Collections.Generic;

public sealed class Buffer : IList<int> {
    readonly int[] items = new int[8];
    int count;

    public int Length => items.Length;

    public int Count => count;

    public bool IsReadOnly => false;

    public int this[int index] {
        get => items[index];
        set => items[index] = value;
    }

    public void Add(int item) => items[count++] = item;

    public void Clear() => count = 0;

    public bool Contains(int item) => IndexOf(item) >= 0;

    public void CopyTo(int[] array, int index) => Array.Copy(items, 0, array, index, count);

    public IEnumerator<int> GetEnumerator() {
        for (var i = 0; i < count; i++) {
            yield return items[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(int item) => Array.IndexOf(items, item, 0, count);

    public void Insert(int index, int item) => throw new NotSupportedException();

    public bool Remove(int item) => throw new NotSupportedException();

    public void RemoveAt(int index) => throw new NotSupportedException();
}

public static class Probe {
    public static int Run() {
        var b = new Buffer();
        b.Add(10);
        b.Add(20);
        return b[b.Count - 1];
    }
}
