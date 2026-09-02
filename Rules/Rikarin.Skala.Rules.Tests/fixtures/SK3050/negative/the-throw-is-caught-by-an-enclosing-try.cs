using System;
using System.Threading.Tasks;

// ⚠ An exception the same body catches never reaches the synchronization context, so there is
// nothing to report. The test is deliberately over-broad: any `catch` at all silences it.
public sealed class Panel {
    public async void Refresh() {
        await Task.Yield();

        try {
            throw new InvalidOperationException("recoverable");
        } catch (InvalidOperationException) {
            Console.WriteLine("handled");
        }
    }
}
