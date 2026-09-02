// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
public class AlignDeclaration {
    // `align_multiple_declaration`: the second and later declarators of a *local* declaration take
    // the first declarator's column rather than a continuation level. The key is false in the
    // export, so this fixture is the indent-level shape and the option unit is what flips it.
    //
    // ⚠ The type is deliberately not four columns wide. With `int x = 1, y = 2` the first
    // declarator's column and the continuation indent are the same number, the two layouts are
    // indistinguishable, and the key reads as inert — which is exactly what earlier probes concluded.
    void MultipleDeclarators() {
        System.Int32 firstVariableNameHere = 1,
                     secondVariableNameHere = 2,
                     thirdVariableNameHere = 3,
                     fourthVariableName = 4;
    }

    // ⚠ A *field* with several declarators is the negative control this fixture would like to carry
    // and cannot: the oracle does not move it at either value of the key — which is why
    // AlignsFromOwnColumn excludes a FieldDeclarationSyntax's declaration — but the two engines
    // already disagree about where such a field wraps at all, so the shape would pin SK-DIV-0031
    // instead of this option. The divergence entry carries the reproduction.

    // ⚠ A chained call is not here either. `align_multiline_calls_chain` anchors on the column the
    // chain's first `.` lands on, which moves with the margin, and SK-DIV-0030 records the shape.
}
