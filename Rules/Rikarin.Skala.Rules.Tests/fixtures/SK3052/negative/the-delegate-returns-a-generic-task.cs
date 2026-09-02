using System;
using System.Threading.Tasks;

public sealed class Wiring {
    public void Wire() {
        Func<int, Task<int>> callback = async value => {
            await Task.Yield();
            return value;
        };

        callback(1);
    }
}
