// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #550: an `is` the author broke before or after, over a pattern that can break, puts the pattern one
// level past the operand's line, and the pattern's combinators share that level.

class ABrokenIsOverAPatternChain {
    static bool A(SyntaxKind keyword) =>
        keyword is
            SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword
            or SyntaxKind.AsKeyword;

    static bool B(SyntaxKind keyword) =>
        keyword is
            SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword;

    static bool C(SyntaxKind keyword) =>
        keyword is SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword;

    static bool D(SyntaxKind keyword) =>
        keyword is
            SyntaxKind.NewKeyword;

    static bool E(SyntaxKind keyword) {
        if (keyword is
            SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword) {
            return true;
        }

        var b = keyword is
            SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword;
        Use(
            keyword is
                SyntaxKind.NewKeyword
                or SyntaxKind.IsKeyword
        );
        if (b) {
            return keyword is
                SyntaxKind.NewKeyword
                or SyntaxKind.IsKeyword;
        }

        return keyword is
            not (SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword);
    }

    static bool F(SyntaxToken prev) =>
        prev.IsKind(SyntaxKind.OpenParenToken)
        && prev.Parent
            is ParameterListSyntax { Parameters.Count: 0 }
            or ArgumentListSyntax { Arguments.Count: 0 }
        && Other(prev);

    static bool G(SyntaxKind keyword) =>
        keyword is
            SyntaxKind.NewKeyword or SyntaxKind.IsKeyword;

    static bool H(SyntaxKind keyword) =>
        keyword is
            SyntaxKind.NewKeyword
            and not SyntaxKind.IsKeyword;

    static bool I(object keyword) =>
        keyword is
            Foo { Bar: 1 }
            or Baz;

    static bool J(SyntaxKind keyword, bool other) =>
        other
        && keyword is
            SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword;

    static bool K(SyntaxKind keyword) =>
        keyword
            is SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword;

    static bool L(SyntaxToken prev) {
        return prev.IsKind(SyntaxKind.OpenParenToken)
            && prev.Parent
                is ParameterListSyntax { Parameters.Count: 0 }
                or ArgumentListSyntax { Arguments.Count: 0 };
    }
}
