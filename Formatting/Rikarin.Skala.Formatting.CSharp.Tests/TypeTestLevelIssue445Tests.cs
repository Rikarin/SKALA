namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #445, SK-DIV-0206: a break around <c>is</c> or <c>as</c> lands one level past the line its
///     operand starts on, not one level past everything open there. #440 stacked the type test's own
///     level on an argument list opened on the same line, so <c>nodes.Count(c =&gt; c.Parent</c> /
///     <c>is ArgumentSyntax</c> went eight columns in where the oracle writes four. Every expected
///     string is <c>jb cleanupcode</c>'s own output for the input, and <see cref="Oracle.Agrees" />
///     asserts the second pass too.
/// </summary>
public sealed class TypeTestLevelIssue445Tests {
    const string Long4 = "&& Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgum"
        + "entValueXYZ) is SomeLongType;";

    const string Long1 = "Microsoft.CodeAnalysis.CSharp.Syntax.Argument" + "Syntax";
    const string Long2 = "collection.Parent.SomeVeryLongPropertyNameXYZ" + "WVUTSRQPONMLKJIHGFEDCBA";
    const string Long3 = "collection.Parent.SomeVeryLongPropertyNameXYZ" + "WVUTSRQPONMLKJIHGFEDCBA_AndMoreAndMore";

    /// <summary>
    ///     A kept or added break around <c>is</c> lands one level past the operand's line: in a lambda that
    ///     is an argument, under an operator, after a broken call, in an expression body and a return,
    ///     and a list opened on the operand's line keeps its own level.
    /// </summary>
    [Fact]
    public void TheBreakLandsOneLevelPastTheOperandsLine() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M() {
                      var a10 = collection.Elements.All(static element => element
                          is ExpressionElementSyntax);
                      var a1 = nodes.Count(static collection => collection.Parent
                          is {{Long1}});
                      var a2 = nodes.Count(static collection => {{Long2}} is SomeVeryLongTypeName);
                      var a3 = Compute(collection.Parent
                          is ArgumentSyntax);
                      var a4 = Compute(alpha, {{Long3}} is SomeType);
                      var a5 = nodes
                          .Where(static collection => collection.Parent
                              is ArgumentSyntax)
                          .Count();
                      Use(x => x
                          is string);
                      if (Compute(alpha,
                              beta) is string) { }
                      var a6 = Compute(
                          alpha,
                          beta) is string;
                      var a7 = Compute(
                              alpha,
                              beta)
                          is string;
                      bool a8 = flag
                          {{Long4}}
                  }

                  bool P(object o) => o
                      is string;

                  object Q(object o) {
                      return o
                          is string;
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      var a10 = collection.Elements.All(static element => element
                          is ExpressionElementSyntax
                      );
                      var a1 = nodes.Count(static collection => collection.Parent
                          is {{Long1}}
                      );
                      var a2 = nodes.Count(static collection =>
                          {{Long2}} is SomeVeryLongTypeName
                      );
                      var a3 = Compute(
                          collection.Parent
                              is ArgumentSyntax
                      );
                      var a4 = Compute(
                          alpha,
                          {{Long3}} is SomeType
                      );
                      var a5 = nodes
                          .Where(static collection => collection.Parent
                              is ArgumentSyntax
                          )
                          .Count();
                      Use(x => x
                          is string
                      );
                      if (Compute(
                              alpha,
                              beta
                          ) is string) { }

                      var a6 = Compute(
                          alpha,
                          beta
                      ) is string;
                      var a7 = Compute(
                              alpha,
                              beta
                          )
                          is string;
                      bool a8 = flag
                          && Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValueXYZ
                          ) is SomeLongType;
                  }

                  bool P(object o) =>
                      o
                          is string;

                  object Q(object o) {
                      return o
                          is string;
                  }
              }
              """
        );
}
