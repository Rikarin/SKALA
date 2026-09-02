using System.Runtime.CompilerServices;

public interface IExplicitSink {
    void Log(string message, string caller = "", int level = 0);
}

public sealed class ExplicitSink : IExplicitSink {
    void IExplicitSink.Log(string message, [CallerMemberName] string caller = "", int level = 0) { }
}
