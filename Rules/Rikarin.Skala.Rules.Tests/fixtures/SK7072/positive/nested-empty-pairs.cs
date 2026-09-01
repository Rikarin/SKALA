// ⚠ Both brackets are reported in one pass, deliberately. Reporting only the inner pair would make
// `skala fix` converge over two runs, and a fix that has to be run twice reads as a fix that failed.
public sealed class Work {
    public void Run() { }
}

#pragma warning disable CS0168 // The outer bracket holds only the inner one.
#pragma warning disable CS0219
#pragma warning restore CS0219
#pragma warning restore CS0168
