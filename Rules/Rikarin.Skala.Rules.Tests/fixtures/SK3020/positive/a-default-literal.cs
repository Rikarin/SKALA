using System.Threading.Tasks;

public sealed class Writer {
    public Task Flush(bool dirty) {
        if (!dirty) {
            return default;
        }

        return Task.CompletedTask;
    }
}
