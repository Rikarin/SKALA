using System;
using System.Threading.Tasks;

public delegate void Notify(int value);

public sealed class Wiring {
    public void Wire() {
        Notify notify = async value => {
            await Task.Yield();
            Console.WriteLine(value);
        };

        notify(1);
    }
}
