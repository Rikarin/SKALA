using System.Threading.Tasks;

public sealed class Writer {
    public Task Flush(bool dirty) {
        if (!dirty) {
            return Task.CompletedTask;
        }

        return Task.FromResult(0);
    }
}
