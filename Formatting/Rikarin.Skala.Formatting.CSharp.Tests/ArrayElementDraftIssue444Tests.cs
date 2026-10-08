namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #444, SK-DIV-0208: an array initializer's fill measures an element flat, with the author's
///     kept breaks read as spaces and up to the first line of a comment or literal that spans lines, and
///     the element after one that spanned lines starts a line of its own. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and <see cref="Oracle.Agrees" /> asserts the
///     second pass too.
/// </summary>
public sealed class ArrayElementDraftIssue444Tests {
    const string Long2 = "var a1 = new object[] { alphaValue, betaValue, Compute(alphaArgumentValue, betaA"
        + "rgumentValue, gammaArgumentValue, deltaArgumentValue, epsilon), tail };";

    const string Long3 = "var a2 = new object[] { alphaValue, betaValue, Compute(alphaArgumentValue, betaA"
        + "rgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zeta"
        + "ArgumentValue), tail };";

    const string Long4 = "var a3 = new object[] { alphaValue, betaValue, new[] { alphaArgumentValue, betaA"
        + "rgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zeta"
        + " }, tail };";

    const string Long5 = "var a4 = new object[] { alphaValue, betaValue, new[] { alphaArgumentValue, betaA"
        + "rgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zeta"
        + "ArgumentValue, eta }, tail };";

    const string Long6 = "var a5 = new object[] { alphaValue, betaValue, new List<int> { alphaArgumentValu"
        + "e, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentVal"
        + "ue, zetaArgumentValue }, tail };";

    const string Long7 = "var a6 = new object[] { alphaValue, betaValue, x => Compute(alphaArgumentValue, "
        + "betaArgumentValue, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue,"
        + " zeta), tail };";

    const string Long8 = "var a7 = new object[] { alphaValue, betaValue, alphaArgumentValue + betaArgument"
        + "Value + gammaArgumentValue + deltaArgumentValue + epsilonArgumentValue + zeta, t"
        + "ail };";

    const string Long9 = "var a8 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue), Compute(alphaArgumentValue, betaArgumentValue, gammaArgu"
        + "mentValue), tail };";

    const string Long10 = "Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgument"
        + "Value, epsilon), tail";

    const string Long11 = "alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValue, e"
        + "psilonArgumentValue,";

    const string Long12 = "var c1 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue), tail };";

    const string Long13 = "var c2 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue), Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArg"
        + "umentValue, epsilonArgumentValue, zetaArgumentValue) };";

    const string Long14 = "var c3 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue), Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue) };";

    const string Long15 = "var c4 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue), [\"ss\", @\"verbatim\\path\", \"ssssssssssssss\", null, 3_000_000L, alphaArgumentV"
        + "alue, betaArgumentValue, rest] };";

    const string Long16 = "var c5 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue), new[] { \"ss\", @\"verbatim\\path\", \"ssssssssssssss\", null, 3_000_000L, alphaAr"
        + "gumentValue, betaArgumentValue, rest } };";

    const string Long17 = "var c6 = new object[] { alphaValue, Compute(alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue), [1, 2] };";

    const string Long18 = "var c7 = new object[] { alphaValue, new[] { alphaArgumentValue, betaArgumentValu"
        + "e, gammaArgumentValue, deltaArgumentValue, epsilonArgumentValue, zetaArgumentVal"
        + "ue, eta }, [\"ss\", @\"verbatim\\path\", \"ssssssssssssss\", null, 3_000_000L, alphaArg"
        + "umentValue, betaArgumentValue, rest] };";

    const string Long19 = "var c8 = new object[] { alphaValue, betaValue, [\"ss\", @\"verbatim\\path\", \"sssssss"
        + "sssssss\", null, 3_000_000L, alphaArgumentValue, betaArgumentValue, rest] };";

    const string Long20 = "[\"ss\", @\"verbatim\\path\", \"ssssssssssssss\", null, 3_000_000L, alphaArgumentValue,"
        + " betaArgumentValue, rest]";

    const string Long21 = "\"ss\", @\"verbatim\\path\", \"ssssssssssssss\", null, 3_000_000L, alphaArgumentValue, "
        + "betaArgumentValue, rest";

    const string Long22 = "var d = new[] { Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo,"
        + " gammaArgumentValueNumberThree), 2, 3 };";

    const string Long23 = "var e = new[] { 1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberT"
        + "wo, gammaArgumentValueNumberThree), 3 };";

    const string Long24 = "Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentVa"
        + "lueNumberThree), 2, 3";

    const string Long25 = "1, Compute(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumen"
        + "tValueNumberThree), 3";

    const string Long26 = "(\"SK9099\", \"FormatDiagnosticIds\"), (\"SK9001\", \"SkalaDiagnostic\"), // ConfigDiagn"
        + "osticIds.UnknownKey";

    const string Long1 = "FormatDiagnosticIds.FileIoFailed";

    /// <summary>
    ///     An element is measured flat with its kept breaks read as spaces: <c>Compute(</c> / arguments / <c>)</c> goes back
    ///     beside the elements before it when <c>Compute(alpha, beta)</c> fits there, and an element that does not fit flat
    ///     goes below.
    /// </summary>
    [Fact]
    public void AnElementIsMeasuredFlat_KeptBreaksIgnored() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long2}}
                      {{Long3}}
                      {{Long4}}
                      {{Long5}}
                      {{Long6}}
                      {{Long7}}
                      {{Long8}}
                      {{Long9}}
                      var a9 = new object[] {
                          alphaValue, betaValue, Compute(
                              alphaArgumentValue,
                              betaArgumentValue
                          ), tail
                      };
                      var b1 = new object[] {
                          alphaValue, betaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue
                          ),
                          tail
                      };
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      var a1 = new object[] {
                          alphaValue, betaValue,
                          {{Long10}}
                      };
                      var a2 = new object[] {
                          alphaValue, betaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          tail
                      };
                      var a3 = new object[] {
                          alphaValue, betaValue,
                          new[] {
                              {{Long11}}
                              zeta
                          },
                          tail
                      };
                      var a4 = new object[] {
                          alphaValue, betaValue,
                          new[] {
                              {{Long11}}
                              zetaArgumentValue, eta
                          },
                          tail
                      };
                      var a5 = new object[] {
                          alphaValue, betaValue,
                          new List<int> {
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          },
                          tail
                      };
                      var a6 = new object[] {
                          alphaValue, betaValue,
                          x => Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zeta
                          ),
                          tail
                      };
                      var a7 = new object[] {
                          alphaValue, betaValue,
                          alphaArgumentValue
                          + betaArgumentValue
                          + gammaArgumentValue
                          + deltaArgumentValue
                          + epsilonArgumentValue
                          + zeta,
                          tail
                      };
                      var a8 = new object[] {
                          alphaValue, Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue),
                          Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue), tail
                      };
                      var a9 = new object[] {
                          alphaValue, betaValue, Compute(
                              alphaArgumentValue,
                              betaArgumentValue
                          ),
                          tail
                      };
                      var b1 = new object[] {
                          alphaValue, betaValue, Compute(
                              alphaArgumentValue,
                              betaArgumentValue
                          ),
                          tail
                      };
                  }
              }
              """
        );

    /// <summary>
    ///     The element after one that spans lines starts a line of its own, whatever it is.
    /// </summary>
    [Fact]
    public void TheElementAfterOneThatSpansLines_StartsALine() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long12}}
                      {{Long13}}
                      {{Long14}}
                      {{Long15}}
                      {{Long16}}
                      {{Long17}}
                      {{Long18}}
                      {{Long19}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      var c1 = new object[] {
                          alphaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          tail
                      };
                      var c2 = new object[] {
                          alphaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          )
                      };
                      var c3 = new object[] {
                          alphaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue)
                      };
                      var c4 = new object[] {
                          alphaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          {{Long20}}
                      };
                      var c5 = new object[] {
                          alphaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          new[] {
                              {{Long21}}
                          }
                      };
                      var c6 = new object[] {
                          alphaValue,
                          Compute(
                              alphaArgumentValue,
                              betaArgumentValue,
                              gammaArgumentValue,
                              deltaArgumentValue,
                              epsilonArgumentValue,
                              zetaArgumentValue
                          ),
                          [1, 2]
                      };
                      var c7 = new object[] {
                          alphaValue,
                          new[] {
                              {{Long11}}
                              zetaArgumentValue, eta
                          },
                          {{Long20}}
                      };
                      var c8 = new object[] {
                          alphaValue, betaValue,
                          {{Long20}}
                      };
                  }
              }
              """
        );

    /// <summary>
    ///     A comment that spans lines inside an element is measured to its first line; the element after it starts a line of
    ///     its own.
    /// </summary>
    [Fact]
    public void ACommentInsideAnElement_IsMeasuredToItsFirstLine() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M() {
                      var b1 = new[] { 1, Compute(2, /* a
                        b */ 3), 4 };
                      var b2 = new[] { new[] { 1, 2 /* a
                        b */, 3 }, new[] { 4 } };
                      var b3 = new[] {
                          first is string,
                          second
                              is string
                      };
                      var b4 = new[] { 1, Compute(
                          2), 3, 4 };
                      var b5 = new[] { 1, 2, Compute(
                          2), 3, 4, 5 };
                      var b6 = new[] { Compute(
                          2), 3, 4 };
                      int[] b7 = [1, Compute(
                          2), 3];
                  }

                  void N() {
                      var a = new Func<int>[] { () => 1, () => {
                          return 2;
                      }, () => 3 };
                      var b = new[] { 1, 2,
                          3, 4 };
                      var c = new[] {
                          new[] { 1, 2 },
                          new[] { 3, 4 }, new[] { 5 }
                      };
                      {{Long22}}
                      {{Long23}}
                      var g = new[] { 1, x switch {
                          1 => 2,
                          _ => 3
                      }, 4 };
                      var h = new object[] { 1, new {
                          A = 1
                      }, 2 };
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      var b1 = new[] {
                          1, Compute(
                              2, /* a
                                b */
                              3
                          ),
                          4
                      };
                      var b2 = new[] {
                          new[] {
                              1, 2 /* a
                                b */,
                              3
                          },
                          new[] { 4 }
                      };
                      var b3 = new[] {
                          first is string, second
                              is string
                      };
                      var b4 = new[] { 1, Compute(2), 3, 4 };
                      var b5 = new[] { 1, 2, Compute(2), 3, 4, 5 };
                      var b6 = new[] { Compute(2), 3, 4 };
                      int[] b7 = [1, Compute(2), 3];
                  }

                  void N() {
                      var a = new Func<int>[] { () => 1, () => { return 2; }, () => 3 };
                      var b = new[] { 1, 2, 3, 4 };
                      var c = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } };
                      var d = new[] {
                          {{Long24}}
                      };
                      var e = new[] {
                          {{Long25}}
                      };
                      var g = new[] {
                          1, x switch {
                              1 => 2,
                              _ => 3
                          },
                          4
                      };
                      var h = new object[] { 1, new { A = 1 }, 2 };
                  }
              }
              """
        );

    /// <summary>
    ///     Block lambdas, switch expressions, anonymous objects and a raw string as elements: a raw string's first line is its
    ///     measure too.
    /// </summary>
    [Fact]
    public void OtherElementsThatSpanLines() =>
        Oracle.Agrees(
            $$""""
              class C {
                  void M() {
                      var a = new Func<int>[] { () => 1, () => {
                          return 2;
                      }, () => 3 };
                      var b = new[] { 1, 2,
                          3, 4 };
                      var c = new[] {
                          new[] { 1, 2 },
                          new[] { 3, 4 }, new[] { 5 }
                      };
                      {{Long22}}
                      {{Long23}}
                      var f = new[] { "a", """
                          raw
                          """, "b" };
                      var g = new[] { 1, x switch {
                          1 => 2,
                          _ => 3
                      }, 4 };
                      var h = new object[] { 1, new {
                          A = 1
                      }, 2 };
                  }
              }
              """",
            $$""""
              class C {
                  void M() {
                      var a = new Func<int>[] { () => 1, () => { return 2; }, () => 3 };
                      var b = new[] { 1, 2, 3, 4 };
                      var c = new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } };
                      var d = new[] {
                          {{Long24}}
                      };
                      var e = new[] {
                          {{Long25}}
                      };
                      var f = new[] {
                          "a", """
                               raw
                               """,
                          "b"
                      };
                      var g = new[] {
                          1, x switch {
                              1 => 2,
                              _ => 3
                          },
                          4
                      };
                      var h = new object[] { 1, new { A = 1 }, 2 };
                  }
              }
              """"
        );

    /// <summary>
    ///     A <c>//</c> comment between elements is a break in front of the next element, not inside the one before it.
    /// </summary>
    [Fact]
    public void ALineCommentBetweenElements_IsNotAnElementSpanningLines() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M() {
                      foreach (var (id, site) in new[] {
                                   ("SK9098", "ArrangementRule"), // ArrangeIds.Reverted
                                   ("SK9015", "FormatDiagnosticIds"), // {{Long1}}
                                   {{Long26}}
                                   ("SK0201", "ArrangementRule")
                               }) {
                      }
                      var x = new[] {
                          1, // a
                          2, 3
                      };
                      foreach (var (name, description) in new[] {
                                   ("create",
                                       "Accept everything that fires now, replacing any existing baseline."),
                                   ("update",
                                       "Accept what fires now in addition to what is already accepted. Never removes."),
                                   ("show", "What the baseline holds.")
                               }) {
                      }
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      foreach (var (id, site) in new[] {
                                   ("SK9098", "ArrangementRule"), // ArrangeIds.Reverted
                                   ("SK9015", "FormatDiagnosticIds"), // {{Long1}}
                                   {{Long26}}
                                   ("SK0201", "ArrangementRule")
                               }) { }

                      var x = new[] {
                          1, // a
                          2, 3
                      };
                      foreach (var (name, description) in new[] {
                                   ("create",
                                       "Accept everything that fires now, replacing any existing baseline."),
                                   ("update",
                                       "Accept what fires now in addition to what is already accepted. Never removes."),
                                   ("show", "What the baseline holds.")
                               }) { }
                  }
              }
              """
        );
}
