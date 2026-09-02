public sealed class Registry {
    static int count;

    public static void Add() {
        // ⚠ `CA2002`'s as well, and measured the same way: silent in a default build because the
        // rule ships off, fires on `lock (typeof(T))` once its severity is raised. The `Type`
        // instance is shared with every other assembly that names the type, which is a *different*
        // defect from this rule's — there the monitor is too widely shared, here it is not shared
        // at all.
        lock (typeof(Registry)) {
            count++;
        }
    }
}
