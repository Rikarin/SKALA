using System.Threading.Tasks;

public sealed class Writer {
    public Task Flush(bool dirty) {
        if (!dirty) {
            return null;
        }

        return Task.CompletedTask;
    }
}
