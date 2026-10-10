namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #593: an author's break before a chain's held first call, in a chain that is a statement's
///     whole condition, keeps the dots on the aligned column (#495) instead of a level past it. Pass one
///     wrote that break for width and pass two read it as the author's (fuzz seed 16215088427476222539).
///     Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-09 with
///     <c>Testing ask</c> over <c>if</c>, <c>else if</c>, <c>while</c>, <c>do</c>, <c>for</c>, <c>switch</c>,
///     <c>foreach</c>, <c>using</c>, <c>!</c>, <c>&amp;&amp;</c> and both lambda shapes, short and long.
/// </summary>
public sealed class HeldCallInAHeaderIssue593Tests {
    const string Receiver = "source_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww";

    const string Select = ".Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo)";

    /// <summary>Before the fix: every dot four columns past the condition's.</summary>
    [Fact]
    public void AnAuthorsBreakBeforeTheHeldCall_KeepsTheDotsOnTheAlignedColumn() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      if ({{Receiver}}
                          {{Select}}.Where(gamma).Any(p)) { A(); }
                      while ({{Receiver}}
                          .Select(a, b).Where(c).Any(p)) { A(); }
                      do { A(); } while ({{Receiver}}
                          .Select(a, b).Where(c).Any(p));
                      if (a) { } else if ({{Receiver}}
                          .Select(a, b).Where(c).Any(p)) { }
                      if ({{Receiver}}
                          .Any(p)) { A(); }
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      if ({{Receiver}}
                          {{Select}}
                          .Where(gamma)
                          .Any(p)) {
                          A();
                      }

                      while ({{Receiver}}
                             .Select(a, b)
                             .Where(c)
                             .Any(p)) {
                          A();
                      }

                      do {
                          A();
                      } while ({{Receiver}}
                               .Select(a, b)
                               .Where(c)
                               .Any(p));

                      if (a) { } else if ({{Receiver}}
                                          .Select(a, b)
                                          .Where(c)
                                          .Any(p)) { }

                      if ({{Receiver}}
                          .Any(p)) {
                          A();
                      }
                  }
              }
              """
        );

    /// <summary>The controls: a header that is not a whole condition keeps the chain's own level.</summary>
    [Fact]
    public void AnAuthorsBreakInAnotherHeader_KeepsTheChainsOwnLevel() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      switch ({{Receiver}}
                          .Select(a, b).Where(c).Any(p)) { default: break; }
                      if (!{{Receiver}}
                          .Select(a, b).Where(c).Any(p)) { A(); }
                      if (flag && {{Receiver}}
                          .Select(a, b).Where(c).Any(p)) { A(); }
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      switch ({{Receiver}}
                                  .Select(a, b)
                                  .Where(c)
                                  .Any(p)) {
                          default: break;
                      }

                      if (!{{Receiver}}
                              .Select(a, b)
                              .Where(c)
                              .Any(p)) {
                          A();
                      }

                      if (flag
                          && {{Receiver}}
                              .Select(a, b)
                              .Where(c)
                              .Any(p)) {
                          A();
                      }
                  }
              }
              """
        );

    /// <summary>
    ///     A <c>for</c>'s condition is a whole condition too, the author's break and the width's alike.
    ///     Before the fix: every dot four columns past the condition's, and the chopped list with them.
    ///     <c>i &lt; 10 &amp;&amp; source…</c> is the control.
    /// </summary>
    [Fact]
    public void AForsWholeCondition_PutsTheDotsOnTheAlignedColumn() =>
        Oracle.Agrees(
            $$"""
              class T {
                  void M() {
                      for (var i = 0; {{Receiver}}
                          .Select(a).Where(b).Any(p); i++) { A(); }
                      for (var i = 0; {{Receiver}}{{Select}}.Where(gammaArgumentValueNumberThreeeeee).Any(p); i++) { A(); }
                      for (var i = 0; {{Receiver}}.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValueNumberThreeeeee).Any(p); i++) { A(); }
                      for (var i = 0; i < 10 && {{Receiver}}
                          .Select(a).Where(b).Any(p); i++) { A(); }
                  }
              }
              """,
            $$"""
              class T {
                  void M() {
                      for (var i = 0;
                           {{Receiver}}
                           .Select(a)
                           .Where(b)
                           .Any(p);
                           i++) {
                          A();
                      }

                      for (var i = 0;
                           {{Receiver}}
                           {{Select}}
                           .Where(gammaArgumentValueNumberThreeeeee)
                           .Any(p);
                           i++) {
                          A();
                      }

                      for (var i = 0;
                           {{Receiver}}.Select(
                               alphaArgumentValueNumberOne,
                               betaArgumentValueNumberTwo,
                               gammaArgumentValueNumberThreeeeee
                           )
                           .Any(p);
                           i++) {
                          A();
                      }

                      for (var i = 0;
                           i < 10
                           && {{Receiver}}
                               .Select(a)
                               .Where(b)
                               .Any(p);
                           i++) {
                          A();
                      }
                  }
              }
              """
        );
}
