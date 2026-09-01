using System;

class Sink {
    public void Add(Action action) => action();
}

class C {
    readonly Sink sink = new();

    void M() {
        for (var i = 0; i < 3; i++) {
            sink.Add(() => Console.WriteLine(i));
        }
    }
}
