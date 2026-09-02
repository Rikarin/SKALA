public sealed class Bootstrap {
    static object gate = null!;

    // ⚠ The only shape in which a write to the lock gate genuinely sits inside a field declaration:
    // one static field's initializer assigning another on the way past. An instance field cannot do
    // this at all (CS0236), which is why the `FieldDeclarationSyntax` exclusion looked like dead
    // code until this fixture was written for it. The write still runs once, before any thread can
    // reach `Bump`, so it is an initializer write like any other and must not report.
    static readonly bool ready = (gate = new object()) is not null;

    static int count;

    public static void Bump() {
        lock (gate) {
            count += ready ? 1 : 0;
        }
    }
}
