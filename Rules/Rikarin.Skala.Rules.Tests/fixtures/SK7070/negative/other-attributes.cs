using System;
using System.Diagnostics;

// The rule is about one attribute. Every other bare attribute in the file is not its concern.
[Serializable]
[DebuggerDisplay("Store")]
public sealed class Store {
    [Conditional("DEBUG")]
    public void Trace() { }

    [ThreadStatic]
    static int depth;

    public int Depth => depth;
}
