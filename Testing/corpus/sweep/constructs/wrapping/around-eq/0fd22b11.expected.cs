// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
public class AroundEq {
    // resharper_csharp_wrap_before_eq: which side of the `=` a break the formatter *adds* lands on.
    // Milestone 2 could not observe it because M2 never added one; the ordering rule M3 built
    // (GroupFacts.PrefersOuterBreak) adds it, and the two values put it on different lines.
    void Assignment() {
        someRatherLongAssignmentTargetNameHere =
            someOtherRatherLongSourceExpressionName + anotherOperandNameHere + 1234567;
    }

    void Declaration() {
        var someRatherLongDeclaredVariableNameHere = someOtherRatherLongSourceExpressionName + anotherOperandName + 12;
    }
}
