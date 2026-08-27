using System;

public interface IDevice : IDisposable {
    int Id { get; }
}

public sealed class Backend : IDisposable {
    public IDevice OpenDevice() => throw new NotSupportedException();

    public void Dispose() { }
}

public sealed class Host {
    IDevice? _device;

    // ⚠ The outward half of the ownership question, and it took a reference tree to find. The
    // backend is never passed anywhere — and the device it handed out is kept in a field and used
    // long after this method returns. A `using` on the backend would close the device with it.
    public void Open() {
        var backend = new Backend();
        _device = backend.OpenDevice();
    }
}
