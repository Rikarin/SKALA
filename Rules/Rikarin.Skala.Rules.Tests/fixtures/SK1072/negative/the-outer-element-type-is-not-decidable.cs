using System.Collections;
using System.Collections.Generic;

public sealed class Pair : IEnumerable<int>, IEnumerable<string> {
    readonly List<int> numbers = [];

    public void Add(int value) => numbers.Add(value);

    public void Add(string value) => numbers.Add(value.Length);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => numbers.GetEnumerator();

    IEnumerator<string> IEnumerable<string>.GetEnumerator() {
        yield break;
    }

    public IEnumerator GetEnumerator() => numbers.GetEnumerator();
}

public sealed class Codes {
    public Pair All(int fallback) => [.. new[] { 200, 204 }, fallback];
}
