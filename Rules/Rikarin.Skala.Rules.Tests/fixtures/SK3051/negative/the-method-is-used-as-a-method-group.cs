using System;
using System.Threading.Tasks;

// ⚠ Optional parameters do not participate in delegate conversion, so appending one here is CS0123
// at the `=> PollAsync`. The name check is what sees that from another file.
public sealed class Poller {
    public async Task PollAsync() {
        await Task.Delay(50);
    }

    public Func<Task> Handler() => PollAsync;
}
