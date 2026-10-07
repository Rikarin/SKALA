using System;
using System.IO;

// ⚠ #412's audit: `stream.ReadByte` is a delegate over the stream, stored in a field. Disposing the
// stream at the end of `Setup` makes the later call throw ObjectDisposedException.
public static class Probe {
    static Func<int>? s_read;

    static void Setup() {
        var stream = new MemoryStream(16);
        stream.WriteByte(7);
        stream.Position = 0;
        s_read = stream.ReadByte;
    }

    public static int Run() {
        Setup();
        return s_read!();
    }
}
