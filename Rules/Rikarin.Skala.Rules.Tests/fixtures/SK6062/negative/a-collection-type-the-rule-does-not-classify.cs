using System.Collections.Generic;

// A custom collection's `Add` may be the point of the call. The closed table is what makes
// "nothing reads it" mean the collection is dead rather than that the analyzer could not see.
public sealed class Recorder<T> {
    readonly List<T> items = [];

    public void Add(T item) {
        items.Add(item);
        System.Console.WriteLine(item);
    }
}

public static class Custom {
    public static int Run(IEnumerable<string> items) {
        var recorder = new Recorder<string>();
        foreach (var item in items) {
            recorder.Add(item);
        }

        return 0;
    }
}
