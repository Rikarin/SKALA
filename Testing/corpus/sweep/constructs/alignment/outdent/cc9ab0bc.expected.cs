// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
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
        var total = someLongVariableNameHere
            + anotherLongVariableNameHere
            + yetAnotherLongVariableNameHere
            + oneFinalVariableName;
    }

    void BinaryConditions(int flag) {
        var matched = someLongVariableNameHere > 1
            && anotherLongVariableNameHere > 2
            && yetAnotherLongVariableName > 33333;
    }

    void BinaryPatterns(object candidate) {
        var matched = candidate is > 1000000
        and < 200000000
        and not 30000000
        and not 40000000
        and not 500000
        and not 60000
        and not 700000
        and not 8000000;
    }

    void ChainedCalls() {
        var result = someCollectionOfThingsHere.Where(item => item.IsEnabled)
            .Select(item => item.Name)
            .OrderBy(name => name)
            .ToList();
    }

    // ⚠ The mixed-width chain is still not here, and the header's question it was meant to settle —
    // **is one chain-wide outdent amount enough?** — now has a measured answer anyway: **no**. The
    // shape is a *nested* conditional access, `a?.B()?.C().D()`, and it was unreachable in both
    // engines' agreement until SK-DIV-0030 and SK-DIV-0065 were fixed. Asked at both values, the
    // oracle outdents **per line, by that line's own leading operator**, and Skala spends one amount
    // for the whole chain:
    //
    //     outdent_dots = false      oracle, = true        Skala, = true
    //     a?.B()                    a?.B()                a?.B()
    //         ?.C()                   ?.C()   12 → 10        ?.C()   12 → 11  ← one column, not two
    //         .D()                   .D()    12 → 11        .D()    12 → 11
    //
    // That is SK-DIV-0069, and it is why the shape stays out: `resharper_csharp_outdent_dots` is
    // Tier A and Conformant, and a fixture carrying this would demote it on the strength of a
    // divergence in the outdent arithmetic rather than in anything this file is about. The entry
    // carries the reproduction. ⚠ Note that the *plain* `a?.B().C()` the SK-DIV-0030 entry named
    // would not have shown it either: under `wrap_before_first_method_call = false` the leading `?.`
    // is the first invoked dot, so it never starts a wrapped line, every wrapped line of that chain
    // begins with a one-column `.`, and it moves 12 → 11 — the same answer to the column as
    // `ChainedCalls` above. The nested `?` is the only two-column operator that reaches the start of
    // a line at this export's values.

    // ⚠ Not an outdent shape at either value, and here so that the fixture says so. The operator is
    // at the end of its line under `wrap_before_binary_opsign = false`, and a trailing operator is
    // not something the line after it can be moved by.
    void NotACandidate() {
        var text = someLongVariableNameHere.ToString()
            + anotherLongVariableNameHere.ToString()
            + "a string literal here";
    }
}
