// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #575: a case label whose `when` is on a line of its own nests its pattern's braces from the label's continuation line.

class C575 {
    static object A(object body) {
        switch (body) {
            case InvocationExpressionSyntax {
                    Expression:
                    MemberAccessExpressionSyntax {
                        RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                    } access
                } invocation
                when Array.IndexOf(Materializers, access.Name.Identifier.ValueText) >= 0
                && invocation.ArgumentList.Arguments.Count == 0:
                return invocation;
        }

        return null;
    }

    static object B(object body) {
        switch (body) {
            case InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access
            } invocation:
                return invocation;
        }

        return null;
    }

    static object C(object body) {
        switch (body) {
            case InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access
            } invocation when access.Name.Identifier.ValueText.Length > 0:
                return invocation;
        }

        return null;
    }

    static object D(object body) {
        switch (body) {
            case InvocationExpressionSyntax {
                    Expression: MemberAccessExpressionSyntax {
                        RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                    } access
                } invocation
                when access.Name.Identifier.ValueText.Length > 0:
                return invocation;
        }

        return null;
    }

    static object E(object body) {
        switch (body) {
            case InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access
            } invocation when access.Name.Identifier.ValueText.Length > 0:
                return invocation;
        }

        return null;
    }

    static object F(object body) {
        switch (body) {
            case (
                int aaaaaaaaaaaaaaaaaaaaaaa,
                int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                ) pair
                when pair.aaaaaaaaaaaaaaaaaaaaaaa > 0:
                return pair;
        }

        return null;
    }
}
