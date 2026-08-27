using System;
using System.IO;

public sealed class Deferred {
    // The delegate may run long after this method returns, so when the object is still needed is
    // not a question the rule can answer.
    public Func<int> Plan(string path) {
        var stream = new FileStream(path, FileMode.Open);
        return () => stream.ReadByte();
    }
}
