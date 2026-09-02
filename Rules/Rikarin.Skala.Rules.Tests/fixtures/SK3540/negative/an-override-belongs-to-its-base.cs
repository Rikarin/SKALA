using System.IO;

// The base declares it abstract, so the base has no body to judge; the derived declaration is
// an `override`, so the base is where a missing contract would live. Neither is reported.
public abstract class Resource {
    public abstract void Dispose();
}

public sealed class FileResource : Resource {
    readonly MemoryStream handle = new();

    public override void Dispose() {
        handle.Dispose();
    }
}
