using System;

public sealed class Channel : IDisposable {
    public string Name { get; set; } = "";

    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ The same bug with no name to assign through: `using (new Channel { … })` declares nothing,
    // so there is nowhere for the hoisted assignment to go.
    public void Open() {
        using (new Channel { Name = "main" }) {
            Console.WriteLine("open");
        }
    }
}
