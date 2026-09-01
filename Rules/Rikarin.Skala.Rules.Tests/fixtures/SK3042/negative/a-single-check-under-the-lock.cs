public sealed class Settings {
    static readonly object Gate = new();

    static Settings? instance;

    public static Settings Instance {
        get {
            // No outer read at all, so nothing is read without the lock and `volatile` buys
            // nothing. Slower on the common path and entirely correct.
            lock (Gate) {
                if (instance == null) {
                    instance = new Settings();
                }

                return instance;
            }
        }
    }
}
