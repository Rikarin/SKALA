// #549: an arm the author broke after (or before) its `=>` nests a property pattern's braces, a list
// pattern's brackets and a `when` clause's argument list from the arm's continuation line. A
// subpattern's value stays on its name's line while its head fits, and a pattern chain there takes no
// level of its own.
class SwitchArmHeadUnderAKeptArrowBreak {
    bool A(object owner, bool empty) =>
        owner switch {
            ArgumentListSyntax {
                Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } }
            } =>
                !empty,
            _ => false
        };

    bool B(object owner, object prev) =>
        owner switch {
            CollectionExpressionSyntax when prev is {
                RawKind: (int)SyntaxKind.CloseParenToken, Parent: CastExpressionSyntax
            } =>
                true,
            _ => false
        };

    bool C(object owner) =>
        owner switch {
            BlockSyntax { Parent: AnonymousMethodExpressionSyntax or SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax } =>
                true,
            _ => false
        };

    int D(object owner) =>
        owner switch {
            ArgumentListSyntax { Parent: InvocationExpressionSyntax } =>
                1,
            _ => 0
        };

    int E(object owner) =>
        owner switch {
            ArgumentListSyntax {
                Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } }
            }
                => 1,
            _ => 0
        };

    int F(object owner) =>
        owner switch {
            ArgumentListSyntax {
                Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } }
            } => 1,
            _ => 0
        };

    void G(object owner) {
        var x = owner switch {
            ArgumentListSyntax {
                Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } }
            } =>
                1,
            _ => 0
        };
    }

    int H(object owner) =>
        owner switch {
            ArgumentListSyntax a when Compute(a, owner, owner, owner, owner, owner, owner, owner, owner, owner, owner, owner) =>
                1,
            _ => 0
        };

    int I(int[] owner) =>
        owner switch {
            [
                1,
                2
            ] =>
                1,
            _ => 0
        };

    int J(object owner) =>
        owner switch {
            Foo {
                Parent: InvocationExpressionSyntax
            }
                => 1,
            _ => 0
        };

    int K(object owner) =>
        owner switch {
            ArgumentListSyntax { Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax } } => SomeVeryLongMethodName(owner),
            _ => 0
        };

    bool L(object owner) =>
        owner is { Parent: AnonymousMethodExpressionSyntax or SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax or Foo };

    bool M(object candidate) {
        var matched = candidate is { OnlySubpatternPropertyName: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Text: "x" } } };
        return matched;
    }

    bool N(object candidate) {
        var matched = candidate is { OnlySubpatternPropertyNameIsLong: SomeTypeName.SomeMemberName.SomeOtherMemberName.AndAnotherOne };
        return matched;
    }

    public static bool O(IOperation operation) =>
        operation.Parent is IArgumentOperation {
            Parameter:
            { Ordinal: 0, RefKind: RefKind.Ref, ContainingSymbol: IMethodSymbol { IsExtensionMethod: true } },
            Parent: IInvocationOperation
        };
}
