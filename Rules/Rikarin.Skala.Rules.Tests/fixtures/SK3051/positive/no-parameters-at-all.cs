using System.Threading;
using System.Threading.Tasks;

// ⚠ The empty parameter list is its own insertion point: the edit lands on the closing paren.
public sealed class Poller {
    public async Task PollAsync() {
        await Task.Delay(50);
    }
}
