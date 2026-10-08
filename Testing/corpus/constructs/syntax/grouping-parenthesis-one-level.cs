// A grouping parenthesis spends no level of its own beside whatever opened on its line (#481,
// SK-DIV-0118, SK-DIV-0150): `var x = (c` / `? a`, `var y = ((a` / `+ b))`, `var t = ((` / `1, 2))`,
// `var f = ((x,` / `y) => { })` and `int[] z = ([` / `1,` put the contents one level past the
// statement, the grouping, the `=` and the construct inside being one line's one level. It spends a
// second level only when a construct broke after it lifts it — `var b = ((` / `1 + 2)` / `* 3);` —
// and inside a statement condition, whose own parenthesis is a level of its own.
namespace P;

public class C {
    public void M(int a, int b, bool c, object[] o) {
        var f = ((x,
y) => { });
        var t = ((
1, 2));
        M2(((x,
y) => { }));
        var x = (c
? a
: b);
        var y = ((a
+ b));
        var b2 = ((
1 + 2)
* 3);
        if ((a
== b)) { }
        var d = (((ax * ax)
+ (az
* az))
* ((bx * cz) - (cx * bz)))
- (((bx * bx
)
+ (bz * bz))
* ((ax * cz) - (cx * az)));
        var e = ((b.X - a.X) * (c.Z - a.Z))
- ((c.X
- a.X)
* (b.Z - a.Z));
        var g = (parent >= 0
? nodes[parent].Cost
: 0f)
+ costs[action];
        var h = placement.TriangleOffset
+ (meshlet
.TriangleCount
* 3);
    }
}
