using System.Threading;

public sealed class Settings {
    static Settings? instance;

    public static Settings Instance {
        get {
            if (instance == null) {
                lock (typeof(Settings)) {
                    if (instance == null) {
                        // Already ordered, and not a plain assignment. Adding `volatile` here would
                        // be advice to change code that does not need it.
                        Interlocked.CompareExchange(ref instance, new Settings(), null);
                    }
                }
            }

            return instance;
        }
    }
}
