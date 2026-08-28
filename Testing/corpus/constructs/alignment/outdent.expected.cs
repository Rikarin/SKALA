// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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

    // ⚠ A chain whose links are not all one column wide — `a?.B().C()`, where the first dot is two
    // columns and the rest are one — is the shape the chain-wide amount approximates, and it is
    // deliberately NOT in this fixture: Skala does not chop such a chain at all, at either value of
    // any key here, so the file would pin SK-DIV-0030 rather than these three options. The
    // divergence entry carries the reproduction.

    // ⚠ Not an outdent shape at either value, and here so that the fixture says so. The operator is
    // at the end of its line under `wrap_before_binary_opsign = false`, and a trailing operator is
    // not something the line after it can be moved by.
    void NotACandidate() {
        var text = someLongVariableNameHere.ToString()
            + anotherLongVariableNameHere.ToString()
            + "a string literal here";
    }
}
