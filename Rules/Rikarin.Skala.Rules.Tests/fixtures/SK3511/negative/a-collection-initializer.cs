using System;
using System.Collections;
using System.Collections.Generic;

public sealed class Batch : IDisposable, IEnumerable<int> {
    readonly List<int> items = [];

    public void Add(int item) => items.Add(item);

    public IEnumerator<int> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() { }
}

public sealed class Consumer {
    // A collection initializer hoists to `Add` calls, which is a different rewrite than this rule
    // performs; it is not attempted rather than attempted badly.
    public void Send() {
        using var batch = new Batch { 1, 2, 3 };
        Console.WriteLine(batch);
    }
}
