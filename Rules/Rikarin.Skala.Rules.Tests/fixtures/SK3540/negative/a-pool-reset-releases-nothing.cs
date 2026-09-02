// The deliberate non-interface `Dispose()`: a pooled buffer's reset, named for the caller's
// convenience. Nothing is released here, so there is no cleanup that fails to run.
public sealed class PooledBuffer {
    byte[] storage = new byte[64];
    int length;

    public void Append(byte value) {
        storage[length++] = value;
    }

    public void Dispose() {
        length = 0;
        storage = new byte[64];
    }
}
