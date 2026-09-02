using System;

// The hook is filled in, so the call runs. This is the shape the positive fixtures are missing.
partial class Importer {
    partial void OnCreated();

    public void Create() {
        OnCreated();
    }
}

partial class Importer {
    partial void OnCreated() => Console.WriteLine("created");
}
