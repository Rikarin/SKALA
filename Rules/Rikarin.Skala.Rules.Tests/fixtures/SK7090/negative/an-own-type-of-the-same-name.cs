namespace Vendor.Diagnostics;

// The type is resolved by the semantic model and never matched on the written name.
public sealed class NotImplementedException : System.Exception;

public sealed class Adapter {
    public void Run() => throw new NotImplementedException();
}
