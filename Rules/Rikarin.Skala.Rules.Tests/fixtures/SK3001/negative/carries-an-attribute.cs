using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandAttribute : Attribute { }

public sealed class Commands {
    [Command]
    public async void Reload() {
        await Task.Delay(1);
    }
}
