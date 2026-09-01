using System;

sealed class SuppressFinalizeFixture : IDisposable {
    public void Dispose() => GC.SuppressFinalize(this);
}
