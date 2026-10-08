namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A broken <c>is</c> over a breakable pattern (issue #550) and an arm's head under a kept arrow break
///     (issue #549). <c>constructs/indentation/a-broken-is-over-a-pattern-chain.cs</c> and
///     <c>constructs/breaks/switch-arm-head-under-a-kept-arrow-break.cs</c>.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration. Found reformatting Skala's own <c>SpaceRules.cs</c>,
///     <c>PrimaryConstructorWrites.cs</c> and <c>ReflectiveTypeTestAnalyzer.cs</c>.
/// </remarks>
public sealed class BrokenIsAndArmHeadIssue549550Tests {
    const string Long1 = "is not { Name: \"Enumerable\", Namespace: { Name: \"Linq\", Parent: { Name: \"System\""
        + ", IsGlobalNamespace: true } } };";

    const string Long2 = "Name: \"Enumerable\", Namespace: { Name: \"Linq\", Parent: { Name: \"System\", IsGloba"
        + "lNamespace: true } }";

    const string Long3 = "is not { Name: \"Enumerable\", Namespace: { Name: \"Linq\", Parent: { Name: \"System\""
        + ", IsGlobalNamespace: true } } }) {";

    const string Long4 = "Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifi"
        + "er.Text: \"nameof\" } }";

    const string Long5 = "BlockSyntax { Parent: AnonymousMethodExpressionSyntax or SimpleLambdaExpressionS"
        + "yntax or ParenthesizedLambdaExpressionSyntax } =>";

    const string Long6 = "{ Ordinal: 0, RefKind: RefKind.Ref, ContainingSymbol: IMethodSymbol { IsExtensio"
        + "nMethod: true } },";

    const string Long7 = "owner is { Parent: AnonymousMethodExpressionSyntax or SimpleLambdaExpressionSynt"
        + "ax or ParenthesizedLambdaExpressionSyntax or Foo };";

    const string Long8 = "Ordinal: 0, RefKind: RefKind.Ref, ContainingSymbol: IMethodSymbol { IsExtensionM"
        + "ethod: true }";

    [Fact]
    public void ABrokenIs_PutsThePatternAndItsCombinatorsOneLevelPastTheOperandsLine() =>
        Oracle.Agrees(
            $$"""
              class C {
                  static bool A(SyntaxKind keyword) =>
                      keyword is
                      SyntaxKind.NewKeyword
                          or SyntaxKind.IsKeyword;

                  static bool E(SyntaxKind keyword) {
                      return keyword is
                          SyntaxKind.NewKeyword
                              or SyntaxKind.IsKeyword;
                  }

                  static bool F(SyntaxToken prev) =>
                      prev.IsKind(SyntaxKind.OpenParenToken)
                      && prev.Parent
                      is ParameterListSyntax { Parameters.Count: 0 }
                      or ArgumentListSyntax { Arguments.Count: 0 }
                      && Other(prev);

                  static bool N(SyntaxKind keyword) {
                      return keyword is
                          not (SyntaxKind.NewKeyword
                          or SyntaxKind.IsKeyword);
                  }

                  bool C8(Foo b) =>
                      b.Containing
                      {{Long1}}
              }
              """,
            $$"""
              class C {
                  static bool A(SyntaxKind keyword) =>
                      keyword is
                          SyntaxKind.NewKeyword
                          or SyntaxKind.IsKeyword;

                  static bool E(SyntaxKind keyword) {
                      return keyword is
                          SyntaxKind.NewKeyword
                          or SyntaxKind.IsKeyword;
                  }

                  static bool F(SyntaxToken prev) =>
                      prev.IsKind(SyntaxKind.OpenParenToken)
                      && prev.Parent
                          is ParameterListSyntax { Parameters.Count: 0 }
                          or ArgumentListSyntax { Arguments.Count: 0 }
                      && Other(prev);

                  static bool N(SyntaxKind keyword) {
                      return keyword is
                          not (SyntaxKind.NewKeyword
                          or SyntaxKind.IsKeyword);
                  }

                  bool C8(Foo b) =>
                      b.Containing
                          is not {
                              {{Long2}}
                          };
              }
              """
        );

    [Fact]
    public void ABrokenIs_AfterAChainHeadedByAParenthesis_KeepsTheOperandsColumn() =>
        Oracle.Agrees(
            $$"""
              class C10 {
                  void A(bool a, Foo b, Foo c) {
                      if (a
                          || (b ?? c).Original.Containing
                          {{Long3}}
                          return;
                      }

                      if (a
                          || b.Original.Containing
                          {{Long3}}
                          return;
                      }
                  }
              }
              """,
            $$"""
              class C10 {
                  void A(bool a, Foo b, Foo c) {
                      if (a
                          || (b ?? c).Original.Containing
                          is not {
                              {{Long2}}
                          }) {
                          return;
                      }

                      if (a
                          || b.Original.Containing
                              is not {
                                  {{Long2}}
                              }) {
                          return;
                      }
                  }
              }
              """
        );

    [Fact]
    public void AnArmTheAuthorBrokeAfterItsArrow_NestsItsBracesFromTheArmsContinuationLine() =>
        Oracle.Agrees(
            $$"""
              class C {
                  bool A(object owner, bool empty) =>
                      owner switch {
                          ArgumentListSyntax {
                              {{Long4}}
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
                          {{Long5}}
                              true,
                          _ => false
                      };
              }
              """,
            """
            class C {
                bool A(object owner, bool empty) =>
                    owner switch {
                        ArgumentListSyntax {
                                Parent: InvocationExpressionSyntax {
                                    Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }
                                }
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
                        BlockSyntax {
                                Parent: AnonymousMethodExpressionSyntax
                                or SimpleLambdaExpressionSyntax
                                or ParenthesizedLambdaExpressionSyntax
                            } =>
                            true,
                        _ => false
                    };
            }
            """
        );

    [Fact]
    public void ASubpatternsValue_StaysOnItsNamesLineWhileItsHeadFits() =>
        Oracle.Agrees(
            $$"""
              class C {
                  public static bool O(IOperation operation) =>
                      operation.Parent is IArgumentOperation {
                          Parameter:
                          {{Long6}}
                          Parent: IInvocationOperation
                      };

                  bool G(object owner) =>
                      {{Long7}}
              }
              """,
            $$"""
              class C {
                  public static bool O(IOperation operation) =>
                      operation.Parent is IArgumentOperation {
                          Parameter: {
                              {{Long8}}
                          },
                          Parent: IInvocationOperation
                      };

                  bool G(object owner) =>
                      owner is {
                          Parent: AnonymousMethodExpressionSyntax
                          or SimpleLambdaExpressionSyntax
                          or ParenthesizedLambdaExpressionSyntax
                          or Foo
                      };
              }
              """
        );
}
