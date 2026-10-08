// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// #561: a kept break after a subpattern's colon breaks the value's braces open.

class C561 {
    void A(object expression) {
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access
            } call) {
            return;
        }

        // joined would be 117
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpr
                } access
            } call2) {
            return;
        }

        // joined would be 119
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpres
                } access
            } call3) {
            return;
        }

        // joined would be 120
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpress
                } access
            } call4) {
            return;
        }

        // joined would be 121
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpressi
                } access
            } call5) {
            return;
        }

        // short, kept break
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: 1
                } access
            } call6) {
            return;
        }
    }

    bool B(object o) =>
        o is Foo {
            Expression:
            MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpressionXXXXXXXXXXXXX
            } access
        };

    bool C(object o) =>
        o is Foo {
            Expression:
            Bar {
                A: 1
            }
        };
}
