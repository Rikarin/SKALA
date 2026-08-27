using System.Threading.Tasks;

public sealed class Gate {
    public void Wait() { }
}

public sealed class Runner {
    public async Task RunAsync(Gate gate) {
        gate.Wait();
        await Task.Yield();
    }
}
