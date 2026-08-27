using System;
using System.Threading.Tasks;

public sealed class Scheduler {
    public void Schedule(Action work) { }

    public void Start() {
        Schedule(Tick);
    }

    async void Tick() {
        await Task.Delay(1);
    }
}
