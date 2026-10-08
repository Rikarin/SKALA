// A switch expression governed by a chain whose head is parenthesised (#470, SK-DIV-0158): the chain
// spends its own level even under an arrow that already broke, the head's contents lift past the dots,
// and the arms nest from the dots' line with the `}` on it. A chain with an ordinary head keeps its arms
// at the statement's level, and the same parenthesised chain with no switch after it keeps its dots on
// the `(`'s column under an arrow — the controls.
namespace P;

public class C {
    object A1(object a) =>
(
a).B().C() switch { _ => a };
    object A5(object a) =>
(
a).B
.C() switch { _ => a };
    object A9(object a, object b) =>
(a + b).C()
.D() switch { _ => a };
    object A10(object a, object b) =>
(a + b).C
.D switch { _ => a };
    object A2(object a) =>
a.B()
.C() switch { _ => a };
    object A6(object a) =>
(
a).B().C();
    void M(object a, object b) {
        var x4 = (
a).B()
.C() switch { _ => a };
        var x5 = (a + b).C()
.D() switch { _ => a };
        var x6 = (
a).B
.C switch { _ => a };
        var x7 = (
a)
.B switch { _ => a };
        var x8 = (
a) switch { _ => a };
        var x3 = a.B()
.C() switch { _ => a };
    }
}
