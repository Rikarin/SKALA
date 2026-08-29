// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-29
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

    // ⚠ The mixed-width chain this fixture wanted from the start, and could not have until
    // SK-DIV-0030 was fixed. It answers the question the header leaves open — whether **one
    // chain-wide outdent amount is enough** — and the answer is no: measured at both values, the
    // oracle outdents **per line, by that line's own leading operator**.
    //
    //     outdent_dots = false          outdent_dots = true
    //     a?.B()                        a?.B()
    //         ?.C()                       ?.C()      ← 12 → 10, two columns
    //         .D()                       .D()        ← 12 → 11, one column
    //
    // ⚠ It has to be a *nested* conditional access, and the plain `a?.B().C()` the divergence entry
    // named would not have worked. Under `wrap_before_first_method_call = false` the leading `?.` is
    // the first invoked dot, so it is never a break point and never starts a wrapped line; every
    // wrapped line of that chain begins with a one-column `.` and moves 12 → 11, the same answer to
    // the column as `ChainedCalls` above. The second `?` is the only two-column operator that
    // reaches the start of a line at this export's values.
    void MixedWidthChain() {
        var result = someCollectionOfThingsHere?.WhereEnabled()
            ?.SelectName(item => item.Name)
            .OrderByName(name => name)
            .ToList();
    }

    // ⚠ Not an outdent shape at either value, and here so that the fixture says so. The operator is
    // at the end of its line under `wrap_before_binary_opsign = false`, and a trailing operator is
    // not something the line after it can be moved by.
    void NotACandidate() {
        var text = someLongVariableNameHere.ToString()
            + anotherLongVariableNameHere.ToString()
            + "a string literal here";
    }
}
