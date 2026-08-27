using System.IO;

public sealed class Publisher {
    // The callee may keep it. Passing the object anywhere is a way of handing ownership on, and the
    // rule cannot follow it without inter-procedural analysis.
    public void Publish(byte[] payload) {
        var buffer = new MemoryStream(payload.Length);
        Consume(buffer);
    }

    static void Consume(Stream stream) { }
}
