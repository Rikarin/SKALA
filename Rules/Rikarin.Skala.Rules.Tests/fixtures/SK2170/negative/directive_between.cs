// Under an `#if` the two statements are not necessarily both in the program, and the indentation of
// a conditionally compiled region is a convention rather than a claim.
//
// ⚠ The first version of this fixture put `Publish()` *inside* the `#if`, where the symbol is
// undefined and the statement is disabled text — so the block held one statement, no pair was
// compared, and a sabotage removing the directive check turned nothing red. The region has to sit
// *between* two live statements for the guard to be reachable at all.
class C {
    void M(bool stale) {
        if (stale)
            Reload();
#if TRACE
            Trace();
#endif
            Publish();
    }

    static void Reload() { }

    static void Trace() { }

    static void Publish() { }
}
