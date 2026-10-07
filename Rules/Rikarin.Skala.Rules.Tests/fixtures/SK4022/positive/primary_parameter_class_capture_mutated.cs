// A mutating call on a class-typed capture changes the object the capture points at, never the
// capture, so `readonly` changes nothing (#412). The same holds for `T : class`.
public sealed class Log {
    public int Lines;

    public void Write() => Lines++;
}

public interface ISink {
    void Write();
}

public sealed class Sink : ISink {
    public int Lines;

    public void Write() => Lines++;
}

struct Writer<T>(Log log, T sink)
    where T : class, ISink {
    public void Write() {
        log.Write();
        sink.Write();
    }
}

public static class Probe {
    public static int Run() {
        var log = new Log();
        var sink = new Sink();
        new Writer<Sink>(log, sink).Write();
        return log.Lines + sink.Lines;
    }
}
