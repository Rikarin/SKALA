using System.Threading.Tasks;

public sealed class Writer {
    // `ValueTask` is a struct; `default` is a completed task, not a null one, so there is nothing
    // for `await` to dereference.
    public ValueTask Flush(bool dirty) {
        if (!dirty) {
            return default;
        }

        return new ValueTask(Task.CompletedTask);
    }
}
