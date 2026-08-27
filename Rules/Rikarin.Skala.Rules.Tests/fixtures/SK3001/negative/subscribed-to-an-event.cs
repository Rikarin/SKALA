using System;
using System.Threading.Tasks;

public sealed class Source {
    public event Action? Changed;
}

public sealed class Listener {
    public void Listen(Source source) {
        source.Changed += Refresh;
    }

    async void Refresh() {
        await Task.Delay(1);
    }
}
