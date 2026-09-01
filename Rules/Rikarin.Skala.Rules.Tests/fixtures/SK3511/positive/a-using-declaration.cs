using System;

public sealed class Channel : IDisposable {
    public string Name { get; set; } = "";

    public int Retries { get; set; }

    public void Dispose() { }
}

public sealed class Consumer {
    static string Configured() => "main";

    public void Open() {
        using var channel = new Channel { Name = Configured(), Retries = 3 };
        Console.WriteLine(channel.Name);
    }
}
