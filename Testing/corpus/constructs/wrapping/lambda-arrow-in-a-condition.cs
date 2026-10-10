// A sole lambda as the last operand of an `if` condition (#600, SK-DIV-0377): the line through it ends at the
// header's `)` and its ` {`, so the operand rule decides its arrow there too. The author's breaks inside a
// property pattern's braces do not keep the arrow, and a chain the author broke at `&&` keeps it while its
// first segment fits beside the arrow. Rows from the measured grid, then the issue's own shape.
class C {
    void M() {
        if (flag || !iiiiiiiiiiiiiiiiiiiiii.All(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiii.All(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(x => aaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiii.All(x => aaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && ccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(static node => aaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(static node => aaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(static node => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && cccccccccccccccccc)) {
            return;
        }
        if (flag || !ii.All(static node => aaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccccccc)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(static node => x is AAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiii.All(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiii.All(static node => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiii.All(nnnnnnnnnnnnnnnnnnnn => x is AAA or BBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(nnnnnnnnnnnnnnnnnnnn => x is AAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiii.All(nnnnnnnnnnnnnnnnnnnn => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBB or CCCCCCCCCCCCC)) {
            return;
        }
        if (flag || !iiiiiiiiiiiiiiiiiiiiiiiiiiiiii.All(nnnnnnnnnnnnnnnnnnnn => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBB or CCCCCCCCCCCCCCC)) {
            return;
        }
        if (flag
            || !initializer.Expressions.All(expression => expression is InitializerExpressionSyntax {
                    Expressions.Count: 2
                } item
                && item.Expressions.All(value => model.GetConstantValue(value, cancellation).HasValue)
            )) {
            return;
        }
        if (flag
            || !initializer.Expressions.All(value => value.Kind is SyntaxKind.NumericLiteralExpression
                && value.Parent is not null
            )) {
            return;
        }
    }
}
