using System;
using System.Threading.Tasks;

public sealed class Panel {
    public async void Refresh() {
        await Task.Yield();

        async Task BoomAsync() {
            await Task.Yield();
            throw new InvalidOperationException("faults the task");
        }

        try {
            await BoomAsync();
        } catch (InvalidOperationException) {
            Console.WriteLine("handled");
        }
    }
}
