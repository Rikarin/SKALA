using System.Runtime.CompilerServices;

public interface ISink {
    void Log(string message, string caller = "", int level = 0);
}

public sealed class Sink : ISink {
    public void Log(string message, [CallerMemberName] string caller = "", int level = 0) { }
}
