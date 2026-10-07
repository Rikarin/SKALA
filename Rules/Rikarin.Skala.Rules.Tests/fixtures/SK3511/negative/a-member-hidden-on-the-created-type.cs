// ⚠ #424: inside the initializer `Mode` is looked up on `Derived`; hoisted, `resource.Mode` is looked
// up on the declared `Base`, which `Derived` hides with `new`. #412's audit measured
// "Derived.Mode=fast" becoming "Base.Mode=fast".
using System;

public class Base : IDisposable {
    public string Mode = "";

    public void Dispose() { }
}

public sealed class Derived : Base {
    public new string Mode = "";
}

public static class Probe {
    public static string Run() {
        var seen = "";
        using (Base resource = new Derived { Mode = "fast" }) {
            seen = "Derived.Mode=" + ((Derived)resource).Mode + " Base.Mode=" + resource.Mode;
        }

        return seen;
    }
}
