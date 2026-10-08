// A sole lambda argument in a switch arm's `when` clause under an arrow the author kept on its own line
// (#446, SK-DIV-0212): the list nests from the arm's continuation line, and so do the lambda body's own
// continuation lines — an `is`, an `&&`, a `.Member` — two levels past the arm, `)` at one. Outside an
// arm (`var ok = ….All(e => e` / `is T`) and under a broken chain (`.Where(n => n is not (A` / `or B)` /
// `)` / `.OfType<C>()`) those lines continue the ordinary way.
class C {
    object M(object o) {
        return o switch {
            CollectionExpressionSyntax collection when collection.Elements.All(static element => element
                is ExpressionElementSyntax
            )
                => collection.Elements.OfType<ExpressionElementSyntax>().Select(static element => element.Expression),
            CollectionExpressionSyntax collection when collection.Elements.All(static element => element
                is ExpressionElementSyntax)
                => 1,
            CollectionExpressionSyntax collection when collection.Elements.All(static element => element
                && other
            )
                => 2,
            CollectionExpressionSyntax collection when collection.Elements.All(static element => element
                .Value
            )
                => 3,
            CollectionExpressionSyntax collection when collection.Elements.Any(element => element
                is ExpressionElementSyntax
            )
                => 4,
            X x when Use(a, element => element
                is ExpressionElementSyntax
            )
                => 5,
            _ => null
        };
    }

    void N() {
        var ok = collection.Elements.All(static element => element
            is ExpressionElementSyntax
        );
        if (x) {
            var ok2 = source.Where(static n => n is not (A
                    or B)
                )
                .OfType<C>();
        }
    }
}
