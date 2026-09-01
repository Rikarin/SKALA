public sealed class Settings {
    static readonly object Gate = new();

    static Settings? instance;

    public static Settings Instance {
        get {
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
