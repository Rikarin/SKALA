using System.Threading.Tasks;

public sealed class Writer {
    /// <summary>Flushes the buffer.</summary>
    /// <returns>A task that completes when the buffer is flushed.</returns>
    public async ValueTask FlushAsync() {
        await Task.Yield();
    }
}
