public class Outdent {
    // The `outdent_*` family: a wrapped line that begins with an operator moves *left* by that
    // operator's own width plus the space after it, so the operand behind it keeps the column it
    // would have had. Every key here is false in the export, so this fixture is the un-outdented
    // shape and the option units are what flip each construct to its offset.
    //
    //   outdent_binary_ops          `+`   12 → 10      `&&`  12 → 9
    //   outdent_binary_pattern_ops  `and` 12 →  8
    //   outdent_dots                `.`   12 → 11
    void BinaryOperators() {
        var total = someLongVariableNameHere + anotherLongVariableNameHere + yetAnotherLongVariableNameHere + oneFinalVariableName;
    }

    void BinaryConditions(int flag) {
        var matched = someLongVariableNameHere > 1 && anotherLongVariableNameHere > 2 && yetAnotherLongVariableName > 33333;
    }

    void BinaryPatterns(object candidate) {
        var matched = candidate is > 1000000 and < 200000000 and not 30000000 and not 40000000 and not 500000 and not 60000 and not 700000 and not 8000000;
    }

    void ChainedCalls() {
        var result = someCollectionOfThingsHere.Where(item => item.IsEnabled).Select(item => item.Name).OrderBy(name => name).ToList();
    }

    // ⚠ `a?.B().C()` is still not here, and SK-DIV-0030 is no longer the reason — that is fixed, and
    // `constructs/wrapping/chained-calls.cs` pins it. The reason now is that the shape was measured
    // and **does not distinguish the key's two values**: under `wrap_before_first_method_call =
    // false` the `?.` is the first invoked dot, so it never becomes a break point and never starts a
    // wrapped line. Every wrapped line of such a chain begins with a one-column `.`, and the oracle
    // moves it 12 → 11 — the same answer, to the column, as `ChainedCalls` above. Adding it would
    // have added a method and no evidence.
    //
    // ⚠ The shape that *does* carry the mixed width is a **nested** conditional access,
    // `a?.B()?.C().D()`, where the oracle breaks before the second `?` rather than before its `.`.
    // Measured at both values, it settles the question this fixture's header leaves open — the
    // outdent is **per line, by that line's own leading-operator width**, not one chain-wide amount:
    //
    //     outdent_dots = false          outdent_dots = true
    //     a?.B()                        a?.B()
    //         ?.C()                       ?.C()      ← 12 → 10, two columns
    //         .D()                       .D()        ← 12 → 11, one column
    //
    // It is out of this fixture because Skala breaks before that `.` and not before the `?`, which
    // is a second and separate chain-planner divergence (SK-DIV-0065). A fixture carrying the shape
    // would pin that instead of these three options.

    // ⚠ Not an outdent shape at either value, and here so that the fixture says so. The operator is
    // at the end of its line under `wrap_before_binary_opsign = false`, and a trailing operator is
    // not something the line after it can be moved by.
    void NotACandidate() {
        var text = someLongVariableNameHere.ToString() + anotherLongVariableNameHere.ToString() + "a string literal here";
    }
}
