// ⚠ This fixture exists because a sabotage turned nothing red. Removing the constant-condition
// guard left every other negative fixture passing, which read as "the guard does nothing" — and for
// `while (true)` and `for (;;)` that is true: the first has no identifiers for the variable walk to
// collect and the second has no condition at all, so both are declined a step earlier and the guard
// never sees them.
//
// A `const` local is the shape that does reach it. `ready` binds to an `ILocalSymbol`, so the walk
// collects it as an ordinary local, nothing writes it, and without the guard the rule reports a loop
// whose condition is a compile-time constant — which is the `while (true)` idiom wearing a name.
class C {
    void Pump() {
        const bool ready = true;
        while (ready) {
            System.Console.WriteLine("tick");
        }
    }
}
