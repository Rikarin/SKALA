// A brace block whose opening line also opens a grouping parenthesis — a switch expression, an object,
// collection or array initializer, a `with`, a lambda's block body — nests from that line's level, not
// from the parenthesis's: `var x = (y switch {` puts the arms one level past the statement and the `}`
// on it, exactly where they go without the parenthesis. Skala had them a level deeper because a
// grouping parenthesis's scope is unconditional, which is right for a continuation line inside it
// (`if ((a` / `== b))` is two levels) and wrong for a block on its own line (issue #393, SK-DIV-0148).
// Every shape is written with the parenthesis on a line with other content, and twice with it broken
// onto its own line — after the `=` and after the `(` — and the class nested two deep at the end
// repeats the first shapes at a deeper indent. The companion file, `block-in-a-broken-construct.cs`,
// is the other half of the same rule: the parenthesis does count once the construct inside it broke.
namespace P;

public class T {
    public int Alpha;
    public int Bravo;
    public int Charlie;
    public int Delta;
    public int Echo;
    public int Foxtrot;
    public T() { }
    public T(int a) { Alpha = a; }
    public string M() => "";
}

public record R(int Alpha, int Bravo, int Charlie, int Delta, int Echo);

public class C {
    public string MemberChain(int value) => (value switch { 1 => "a", _ => "b" }).ToString();

    public int Assigned(int y) {
        var x = (y switch { 1 => 10, _ => 0 });
        return x;
    }

    public int AssignedWrittenBroken(int y) {
        var x = (y switch {
                1 => 10,
                _ => 0
            });
        return x;
    }

    public int AfterTheEquals(int y) {
        var x =
            (y switch {
                1 => 10,
                _ => 0
            });
        return x;
    }

    public int AfterTheParenthesis(int y) {
        var x = (
            y switch {
                1 => 10,
                _ => 0
            });
        return x;
    }

    public object ObjectInitializer() {
        var x = (new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000, Foxtrot = 6 });
        return x;
    }

    public object ObjectInitializerAfterTheParenthesis() {
        var x = (
            new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000, Foxtrot = 6 });
        return x;
    }

    public object ObjectInitializerAfterTheEquals() {
        var x =
            (new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000, Foxtrot = 6 });
        return x;
    }

    public object ArrayInitializer() {
        var x = (new[] { 1000000000, 2000000000, 3000000000, 4000000000, 5000000000, 6000000000, 7000000000, 8000000000 });
        return x;
    }

    public object With(R r) {
        var x = (r with { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000 });
        return x;
    }

    public object ConstructorAndInitializer() {
        var x = (new T(1) { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000 });
        return x;
    }

    public object Cast() {
        var x = ((T)new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 50000 });
        return x;
    }

    public object Nested(int y) {
        var x = ((y switch { 1 => 10, _ => 0 }));
        return x;
    }

    public object NestedInitializer() {
        var x = ((new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 50000 }));
        return x;
    }

    public object NestedAfterTheParenthesis(int y) {
        var x = (
            (y switch {
                1 => 10,
                _ => 0
            }));
        return x;
    }

    public object Argument(int y) {
        return N((y switch { 1 => 10, _ => 0 }));
    }

    public object ArgumentAmongOthers(int y) {
        N((y switch { 1 => 10, _ => 0 }), 1);
        return M(1, (y switch { 1 => 10, _ => 0 }));
    }

    public object ArgumentInitializer() {
        return N((new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 50000 }));
    }

    public object LambdaBlockBody() {
        System.Action x = (() => {
            M();
            M();
        });
        return x;
    }

    public object LambdaExpressionBody() {
        System.Func<int, int> f = z => (z switch { 1 => 10, _ => 0 });
        return f;
    }

    public object Negated(int y) {
        var x = !(y switch { 1 => true, _ => false });
        return x;
    }

    public object Converted(int y) {
        var x = (object)(y switch { 1 => 10, _ => 0 });
        return x;
    }

    public string InitializerChain() => (new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5 }).M();

    public string ReturnedChain() {
        return (new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5 }).M();
    }

    public void Statements(int y) {
        M((y switch { 1 => 10, _ => 0 }).ToString());
        _ = (y switch { 1 => 10, _ => 0 });
    }

    public void M() { }
    public void M(string s) { }
    public object M(int a, object b) => b;
    public object N(object a, int b = 0) => a;
}

public class Outer {
    public class Middle {
        public class Deep {
            public string MemberChain(int value) => (value switch { 1 => "a", _ => "b" }).ToString();

            public int Assigned(int y) {
                var x = (y switch { 1 => 10, _ => 0 });
                return x;
            }

            public int AfterTheEquals(int y) {
                var x =
                    (y switch {
                        1 => 10,
                        _ => 0
                    });
                return x;
            }

            public object ObjectInitializer() {
                var x = (new T { Alpha = 1000000, Bravo = 2000000, Charlie = 3000000, Delta = 4000000, Echo = 5000000 });
                return x;
            }

            public object Nested(int y) {
                return N((y switch { 1 => 10, _ => 0 }));
            }

            public object N(object a) => a;
        }
    }
}
