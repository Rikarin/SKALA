public sealed class Settings {
    static readonly object Gate = new();

    static volatile Settings? instance;

    public static Settings Instance {
        get {
            // The idiom written correctly. The `volatile` is the whole of what makes the outer
            // read safe, and this fixture is what proves the rule reads the modifier.
            if (instance == null) {
                lock (Gate) {
                    if (instance == null) {
                        instance = new Settings();
                    }
                }
            }

            return instance;
        }
    }
}
