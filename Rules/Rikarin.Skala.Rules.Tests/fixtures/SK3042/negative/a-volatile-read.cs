using System.Threading;

public sealed class Settings {
    static readonly object Gate = new();

    static Settings? instance;

    public static Settings Instance {
        get {
            // The ordering is done by hand. The rule requires the outer operand to be a plain
            // field reference, which is what keeps it silent here.
            if (Volatile.Read(ref instance) == null) {
                lock (Gate) {
                    if (instance == null) {
                        Volatile.Write(ref instance, new Settings());
                    }
                }
            }

            return instance!;
        }
    }
}
