using System.IO;

// Two declarations, declining for two different reasons. The base's `Dispose` is abstract, so it
// has no body and releases nothing — there is no cleanup that fails to run. The derived one is an
// `override`, so the base is where a missing contract would live and reporting both would say one
// thing twice. ⚠ The reason for the base is `ReleasesSomething` and NOT an `abstract` test: a
// sabotage proved an explicit `abstract` skip here dead, and it was deleted rather than kept.
public abstract class Resource {
    public abstract void Dispose();
}

public sealed class FileResource : Resource {
    readonly MemoryStream handle = new();

    public override void Dispose() {
        handle.Dispose();
    }
}
