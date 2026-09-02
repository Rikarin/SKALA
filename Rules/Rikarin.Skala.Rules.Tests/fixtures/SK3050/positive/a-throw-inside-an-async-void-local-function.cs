using System;
using System.Threading.Tasks;

public sealed class Panel {
    public void Start() {
        async void Run() {
            await Task.Yield();
            throw new InvalidOperationException("nothing to run");
        }

        Run();
    }
}
