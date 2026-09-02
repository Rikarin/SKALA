using System;
using System.Text;

public sealed class Report {
    // ⚠ This file exists because sabotaging the capture guard turned nothing red: the other capture
    // fixture reads the builder inside the lambda, and a read is declined by the classifier whatever
    // it is nested in. Only a capture whose reference is a *pure write* reaches the guard.
    //
    // ⚠ And the guard is conservatism rather than soundness — the buffer really is never read here,
    // so the finding would be true. It is kept because without it the answer depends on brace style:
    // `() => builder.Append(name)` is an expression-bodied lambda whose call result is not discarded
    // and is therefore classified as a read, while `() => { builder.Append(name); }` is a write. A
    // rule that reports one and not the other is reporting the author's formatting.
    public Action Write(string name) {
        var builder = new StringBuilder();
        builder.Append(name);
        return () => { builder.Append('!'); };
    }
}
