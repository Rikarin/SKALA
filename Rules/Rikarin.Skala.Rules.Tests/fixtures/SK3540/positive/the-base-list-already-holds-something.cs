using System.IO;

public abstract class Endpoint {
    public abstract string Name { get; }
}

public sealed class SocketEndpoint : Endpoint {
    readonly MemoryStream channel = new();

    public override string Name => "socket";

    public void Dispose() {
        channel.Close();
    }
}
