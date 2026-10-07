using System.Threading.Tasks;

// #390's measured boundary: a looser scan's only hits were this shape, and documenting the task an
// `async Task` method returns is the established .NET convention.
public sealed class Writer {
    /// <summary>Writes the payload.</summary>
    /// <returns>A task that completes when the payload is written.</returns>
    public async Task WriteAsync() {
        await Task.Yield();
    }
}
