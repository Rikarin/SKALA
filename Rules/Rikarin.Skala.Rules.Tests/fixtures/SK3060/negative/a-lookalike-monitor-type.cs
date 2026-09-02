namespace Fixture.Own;

/// <summary>Somebody's own type that happens to carry the name. It locks nothing.</summary>
public static class Monitor {
    public static void Enter(object target) { }
}

public sealed class Journal {
    readonly object gate = new();

    int entries;

    public void Append() {
        // ⚠ The type is resolved, never matched on the written name. Every other gate in the rule is
        // absent here on purpose — there is no `try`, no release of any kind, and no protocol in the
        // type — so this fixture fails the moment the symbol check stops being the thing that
        // decides. `System.Threading` is not imported, which is what keeps the two names apart.
        Monitor.Enter(gate);
        entries++;
    }
}
