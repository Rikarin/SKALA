using System;

// ⚠ The other half of #306, and the one that made the false positive a class rather than an edge:
// `AppDomain.CurrentDomain.ProcessExit` is the canonical shape-C receiver — an instance event
// reached through a static property, the receiver `the-process-exit-event.cs` reports — and a
// lambda closing over nothing at all publishes no object. The delegate the process keeps holds a
// string literal.
//
// The lambda mentions neither `this` nor any instance member, which is precisely what `Reaches`
// asks and what shape D has always asked of a delegate handed to another thread. The constructor
// also writes a field, so the shape reached here is a real constructor rather than an empty one.
public sealed class Journal {
    readonly string path;

    public Journal(string path) {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Console.WriteLine("bye");
        this.path = path;
    }

    public string Path => path;
}
