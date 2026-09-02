using System;
using System.Threading.Tasks;

public sealed class Wiring {
    public void Wire() {
        Action callback = async delegate {
            await Task.Yield();
        };

        callback();
    }
}
