using System.Threading;

public static class Registry {
    static readonly SpinLock Gate = new(false);

    public static bool Try() {
        var taken = false;
        Gate.Enter(ref taken);
        if (taken) {
            Gate.Exit();
        }

        return taken;
    }
}
