// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #560: a pattern chain that is the left operand of a broken && or || takes its level past the operator's.

class C560 {
    static bool A(SyntaxToken next) {
        var empty = next.Parent is BaseArgumentListSyntax { Arguments.Count: 0 }
                or BaseParameterListSyntax { Parameters.Count: 0 }
            && !HoldsAComment(next);
        return empty;
    }

    static bool B(SyntaxToken next) {
        var empty = next.Parent is BaseArgumentListSyntax
                or BaseParameterListSyntax
            && !HoldsAComment(next);
        return empty;
    }

    static bool C(SyntaxToken next) {
        return next.Parent is BaseArgumentListSyntax
                or BaseParameterListSyntax
            && !HoldsAComment(next);
    }

    static bool D(SyntaxToken next) =>
        next.Parent is BaseArgumentListSyntax
            or BaseParameterListSyntax
        && !HoldsAComment(next);

    static void E(SyntaxToken next) {
        if (next.Parent is BaseArgumentListSyntax
                or BaseParameterListSyntax
            && !HoldsAComment(next)) {
            return;
        }

        Use(
            next.Parent is BaseArgumentListSyntax
                or BaseParameterListSyntax
            && !HoldsAComment(next)
        );
    }

    static bool F(SyntaxToken next) {
        var empty = HoldsAComment(next)
            && next.Parent is BaseArgumentListSyntax
                or BaseParameterListSyntax;
        return empty;
    }

    static bool G(SyntaxToken next) {
        var empty = next.Parent is BaseArgumentListSyntax
                or BaseParameterListSyntax
            || !HoldsAComment(next);
        return empty;
    }

    static bool H(SyntaxToken next) {
        var empty = next.Kind is 1
                or 2
            && !HoldsAComment(next)
            && Other(next);
        return empty;
    }

    internal static bool I(VariableDeclaratorSyntax declarator) =>
        declarator.Parent is VariableDeclarationSyntax {
            Parent:
            LocalDeclarationStatementSyntax
            or FieldDeclarationSyntax
            or EventFieldDeclarationSyntax
        } declaration
        && declaration.Variables[0] == declarator;
}
