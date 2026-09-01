using System.Threading.Tasks;

public sealed class Writer {
    // ⚠ The same bug through a local, and it is not reported: the rule rewrites one span and there
    // is no single span here whose replacement makes the method right.
    public Task Flush(bool dirty) {
        Task? pending = dirty ? Task.CompletedTask : null;
        return pending!;
    }
}
