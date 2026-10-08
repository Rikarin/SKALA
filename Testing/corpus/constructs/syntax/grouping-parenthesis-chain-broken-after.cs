// A grouping parenthesis heading a chain that breaks before a dot after its `)`: the parenthesis's
// contents nest from the chain's continuation line, not from the `(`'s, when that line is deeper
// (issue #470, SK-DIV-0112, SK-DIV-0148, SK-DIV-0159). `var z = (` / `a).B` / `.C();` puts `a` two
// levels past the statement and `.C` one. Where the break before the dot spends nothing — under an
// arrow that already broke, inside an argument list — the contents stay one level past the `(`'s
// line. Rows with a chain group of their own (`.B()` / `[0]` before the broken dot) and a binary
// after the `)` are the controls that were already right.
namespace P;

public class C {
    public object Statements(object a, object b, object y, int q) {
        var z1 = (
a).B
.C();
        (
a).B
.C();
        var z2 = (a
+ b).C
.D();
        var z3 = (
a
).B
.C();
        var z4 = (
a).B()
.C();
        var z5 = (
a)[0]
.C();
        var z6 = (y switch {
1 => 2,
_ => 3
}).ToString()
.Length;
        z1 = (
a).B
.C;
        var z8 = (
a)
+ b;
        var z9 = (
a)
.B;
        var s = q switch {
1 => (
a).B().C(),
2 => (
a) + b,
_ => 0
};
        Call(
(
a).B
.C());
        return (
a).B
.C();
    }

    object A(object a) =>
(
a).B
.C();

    object B(object a, object b) =>
(a
+ b).C
.D();

    object D(object a) => (
a).B
.C();
}
