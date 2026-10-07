using System;
using System.Collections.Generic;
using System.Linq;

namespace Skala.Corpus.Arrangement;

// use_heuristics_for_body_style's sixth condition (#399): what the returned expression holds. A
// switch expression, an anonymous function or an array initializer anywhere in it keeps the block,
// and so does a return whose value is itself an assignment. It is a list of kinds, not a measure —
// the one-line lambda keeps its block and the multi-line query converts.
public class HeuristicsExpression {
    private int _n;
    private int? _m;
    private int[] _arr = new int[1];
    private Func<int> _f;
    private EventHandler _h;

    public int Canary(int a, int b) {
        return a + b;
    }

    public int Switch(int v) {
        return v switch { 1 => 10, _ => 0 };
    }

    public int SwitchInArgument(int v) {
        return Math.Abs(v switch { _ => 0 });
    }

    public int ParenthesizedSwitch(int v) {
        return (v switch { _ => 0 });
    }

    public Func<int, int> Lambda() {
        return x => x + 1;
    }

    public Func<int> LambdaNoParameter() {
        return () => 1;
    }

    public Func<int, int> AnonymousMethod() {
        return delegate(int x) { return x; };
    }

    public int LambdaInArgument(List<int> a) {
        return a.Count(x => x > 1);
    }

    public object LambdaInAnonymousObject() {
        return new { F = (Func<int>)(() => 1) };
    }

    public int[] ImplicitArray() {
        return new[] { 1, 2 };
    }

    public int[] ExplicitArray() {
        return new int[] { 1, 2 };
    }

    public int ArrayInArgument() {
        return Sum(new[] { 1, 2 });
    }

    public Span<int> StackAllocInitializer() {
        return stackalloc[] { 1, 2 };
    }

    public int Assign(int a) {
        return _n = a;
    }

    public int CompoundAssign(int a) {
        return _n += a;
    }

    public int? CoalesceAssign() {
        return _m ??= 1;
    }

    // Converted: the assignment is inside the value, not the value.
    public int AssignInArgument(int a) {
        return Math.Abs(_n = a);
    }

    // Converted: the test is on the value as written, and the parentheses go afterwards.
    public int ParenthesizedAssign(int a) {
        return (_n = a);
    }

    public int[] SizedArray() {
        return new int[2];
    }

    public Exception ObjectInitializer() {
        return new Exception {
            Source = "x",
            HelpLink = "y"
        };
    }

    public List<int> CollectionInitializer() {
        return new List<int> { 1, 2 };
    }

    public object AnonymousObject() {
        return new { A = 1, B = 2 };
    }

    public int[] CollectionExpression() {
        return [1, 2];
    }

    public IEnumerable<int> Query(int[] a) {
        return from x in a
               let y = x + 1
               select y;
    }

    public int MultiLine(int a, int b) {
        return Math.Max(
            a,
            b);
    }

    public int Sum(int[] a) {
        return a.Length;
    }

    // Written broken, because a one-line accessor block whose content wraps is a separate formatter
    // question (#405): the oracle breaks the block open and Skala does not.
    public int GetterSwitch {
        get {
            return _n switch {
                _ => 0
            };
        }
    }

    public Func<int> GetterLambda {
        get { return () => 1; }
    }

    public int GetterAssign {
        get { return _n = 1; }
    }

    // An accessor that is already an arrow collapses onto its owner whatever it holds.
    public Func<int> ArrowGetterLambda {
        get => () => 1;
    }

    public int SetterSwitch {
        get { return _n; }
        set {
            _n = value switch {
                _ => 0
            };
        }
    }

    public int[] SetterArray {
        get { return _arr; }
        set { _arr = new[] { value[0] }; }
    }

    // Converted: an assignment *statement* is the setter's ordinary shape.
    public int SetterAssign {
        get { return _n; }
        set { _n = value; }
    }

    public int InitLambda {
        get { return _n; }
        init { _f = () => value; }
    }

    public event EventHandler E {
        add { _h += (s, e) => { }; }
        remove { _h -= value; }
    }

    public int this[int i] {
        get {
            return i switch {
                _ => 0
            };
        }
    }

    public static HeuristicsExpression operator +(HeuristicsExpression a, HeuristicsExpression b) {
        return a._n switch { _ => a };
    }

    public static implicit operator Func<int>(HeuristicsExpression c) {
        return () => c._n;
    }

    public int Local() {
        return Inner(1) + Inner2(1);

        int Inner(int v) {
            return v switch { _ => 0 };
        }

        int Inner2(int v) {
            return v;
        }
    }
}
