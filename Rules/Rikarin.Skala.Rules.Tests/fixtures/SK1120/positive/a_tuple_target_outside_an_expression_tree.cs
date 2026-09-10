// ⚠ The other half of the tuple guard, and the half that stops it from being over-broad. The
// rewrite here is `x is (int, int)`, which the parser reads as a pattern — but a pattern is only
// illegal *inside an expression tree*. Out here it compiles and means exactly what
// `typeof((int, int)).IsInstanceOfType(x)` means, so the finding stands.
//
// Paired with `negative/a_tuple_target_inside_an_expression_tree.cs`: declining the tuple target
// outright would turn this red, and dropping the guard would turn that one red. Neither fixture
// alone says where the boundary is.
public static class TupleTargetOutsideAnExpressionTree {
    public static bool IsPair(object value) => typeof((int, int)).IsInstanceOfType(value);
}
