using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #438, SK-DIV-0201: at <c>place_*_attribute_on_same_line = always</c> the oracle declines to
///     join an attribute when the joined line overflows by its terminator alone — <c>) { }</c> at 121 with
///     the <c>)</c> at 117, a field whose value ends at 120 before its <c>;</c> — and joins it, letting the
///     declaration wrap inside, otherwise. Skala joined every one. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input under the three keys at <c>always</c>, and a
///     second pass is asserted.
/// </summary>
public sealed class AttributeJoinTerminatorIssue438Tests {
    const string Long41 = "[Obsolete] public abstract void AbstractMethodName(int alphaParameterValue, int "
        + "betaParameterValue, int gammaVa);";

    const string Long42 = "[Obsolete] public abstract void AbstractMethodName(int alphaParameterValue, int "
        + "betaParameterValue, int gammaVal);";

    const string Long43 = "[Obsolete] public abstract void AbstractMethodName(int alphaParameterValue, int "
        + "betaParameterValue, int gammaValu);";

    const string Long44 = "[Obsolete] public abstract void AbstractMethodName(int alphaParameterValue, int "
        + "betaParameterValue, int gammaValueXYZ);";

    const string Long1 = "gxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    const string Long2 = "gxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    const string Long3 = "gxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    const string Long4 = "gxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    const string Long5 = "gxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    const string Long6 = "gxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    const string Long7 = "dddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long8 = "dddddddddddddddddddddddddddddddddddddd";
    const string Long9 = "ddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long10 = "ddddddddddddddddddddddddddddddddddddddd";
    const string Long11 = "dddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long12 = "dddddddddddddddddddddddddddddddddddddddd";
    const string Long13 = "P120xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    const string Long14 = "P121xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        + "x";

    const string Long15 = "P122xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        + "xx";

    const string Long16 = "ddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long17 = "dddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long18 = "ddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long19 = "dddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long20 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long21 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long22 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long23 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long24 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";

    const string Long25 = "P123xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        + "xxx";

    const string Long26 = "P124xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        + "xxxx";

    const string Long27 = "P125xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        + "xxxxx";

    const string Long28 = "P126xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
        + "xxxxxx";

    const string Long29 =
        "P127xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    const string Long30 =
        "P128xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    const string Long31 =
        "P129xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" + "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    const string Long32 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long33 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long34 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long35 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long36 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long37 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long38 = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long39 = "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    const string Long40 = "int, int, int, int, int, int, int, int, int, int, int, int";

    static void Agrees(string source, string expected) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [
                        new KeyValuePair<string, string>("skala_place_method_attribute_on_same_line", "always"),
                        new KeyValuePair<string, string>("skala_place_field_attribute_on_same_line", "always"),
                        new KeyValuePair<string, string>("skala_place_accessorholder_attribute_on_same_line", "always")
                    ]
                )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     Methods with an empty body, a block body and an expression body, fields and properties, each
    ///     widened a column at a time past the margin: joined while the line fits, declined while only the
    ///     <c> { }</c>, <c> {</c> or <c>;</c> is past it, joined and wrapped inside once more is.
    /// </summary>
    [Fact]
    public void MethodsAndFields_DeclineTheJoinOnlyForTheTerminator() =>
        Agrees(
            $$"""
              public class In {
                  [Obsolete] public void Me116_(int alphaParameterValue, int betaParameterValue, int {{Long1}}) { }
                  [Obsolete] public void Me117_(int alphaParameterValue, int betaParameterValue, int {{Long2}}) { }
                  [Obsolete] public void Me118_(int alphaParameterValue, int betaParameterValue, int {{Long3}}) { }
                  [Obsolete] public void Me119_(int alphaParameterValue, int betaParameterValue, int {{Long4}}) { }
                  [Obsolete] public void Me120_(int alphaParameterValue, int betaParameterValue, int {{Long5}}) { }
                  [Obsolete] public void Me121_(int alphaParameterValue, int betaParameterValue, int {{Long6}}) { }
                  [Obsolete] public void Mb116_(int alphaParameterValue, int betaParameterValue, int {{Long1}}) {
                      Foo();
                  }
                  [Obsolete] public void Mb117_(int alphaParameterValue, int betaParameterValue, int {{Long2}}) {
                      Foo();
                  }
                  [Obsolete] public void Mb118_(int alphaParameterValue, int betaParameterValue, int {{Long3}}) {
                      Foo();
                  }
                  [Obsolete] public void Mb119_(int alphaParameterValue, int betaParameterValue, int {{Long4}}) {
                      Foo();
                  }
                  [Obsolete] public void Mb120_(int alphaParameterValue, int betaParameterValue, int {{Long5}}) {
                      Foo();
                  }
                  [Obsolete] public int F119 = 1234567 + 2345678 + 3456789 + 4567890 + {{Long7}};
                  [Obsolete] public int A119(int alphaParameterValue) => alphaParameterValue + {{Long8}};
                  [Obsolete] public int F120 = 1234567 + 2345678 + 3456789 + 4567890 + {{Long9}};
                  [Obsolete] public int A120(int alphaParameterValue) => alphaParameterValue + {{Long10}};
                  [Obsolete] public int F121 = 1234567 + 2345678 + 3456789 + 4567890 + {{Long11}};
                  [Obsolete] public int A121(int alphaParameterValue) => alphaParameterValue + {{Long12}};
                  [Obsolete] public int {{Long13}} { get; set; }
                  [Obsolete] public int {{Long14}} { get; set; }
                  [Obsolete] public int {{Long15}} { get; set; }
              }
              """,
            $$"""
              public class In {
                  [Obsolete] public void Me116_(int alphaParameterValue, int betaParameterValue, int {{Long1}}) { }

                  [Obsolete]
                  public void Me117_(int alphaParameterValue, int betaParameterValue, int {{Long2}}) { }

                  [Obsolete]
                  public void Me118_(int alphaParameterValue, int betaParameterValue, int {{Long3}}) { }

                  [Obsolete]
                  public void Me119_(int alphaParameterValue, int betaParameterValue, int {{Long4}}) { }

                  [Obsolete]
                  public void Me120_(int alphaParameterValue, int betaParameterValue, int {{Long5}}) { }

                  [Obsolete] public void Me121_(
                      int alphaParameterValue,
                      int betaParameterValue,
                      int {{Long6}}
                  ) { }

                  [Obsolete] public void Mb116_(int alphaParameterValue, int betaParameterValue, int {{Long1}}) {
                      Foo();
                  }

                  [Obsolete] public void Mb117_(int alphaParameterValue, int betaParameterValue, int {{Long2}}) {
                      Foo();
                  }

                  [Obsolete] public void Mb118_(int alphaParameterValue, int betaParameterValue, int {{Long3}}) {
                      Foo();
                  }

                  [Obsolete]
                  public void Mb119_(int alphaParameterValue, int betaParameterValue, int {{Long4}}) {
                      Foo();
                  }

                  [Obsolete]
                  public void Mb120_(int alphaParameterValue, int betaParameterValue, int {{Long5}}) {
                      Foo();
                  }

                  [Obsolete] public int F119 = 1234567 + 2345678 + 3456789 + 4567890 + {{Long7}};
                  [Obsolete] public int A119(int alphaParameterValue) => alphaParameterValue + {{Long8}};

                  [Obsolete]
                  public int F120 = 1234567 + 2345678 + 3456789 + 4567890 + {{Long9}};

                  [Obsolete] public int A120(int alphaParameterValue) =>
                      alphaParameterValue + {{Long10}};

                  [Obsolete] public int F121 =
                      1234567 + 2345678 + 3456789 + 4567890 + {{Long11}};

                  [Obsolete] public int A121(int alphaParameterValue) =>
                      alphaParameterValue + {{Long12}};

                  [Obsolete] public int {{Long13}} { get; set; }

                  [Obsolete] public int {{Long14}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long15}} {
                      get;
                      set;
                  }
              }
              """
        );

    /// <summary>
    ///     Fields of identifiers, accessor lists that expand, initialized properties and expression-bodied
    ///     properties: only the field whose value ends at the margin is declined.
    /// </summary>
    [Fact]
    public void FieldsAndProperties_JoinWhenSomethingInsideWraps() =>
        Agrees(
            $$"""
              public class In {
                  [Obsolete] public int F121 = alphaValue + betaValue + gammaValue + {{Long16}};
                  [Obsolete] public int F122 = alphaValue + betaValue + gammaValue + {{Long17}};
                  [Obsolete] public int F123 = alphaValue + betaValue + gammaValue + {{Long18}};
                  [Obsolete] public int F124 = alphaValue + betaValue + gammaValue + {{Long19}};
                  [Obsolete] public int F125 = alphaValue + betaValue + gammaValue + {{Long20}};
                  [Obsolete] public int F126 = alphaValue + betaValue + gammaValue + {{Long21}};
                  [Obsolete] public int F127 = alphaValue + betaValue + gammaValue + {{Long22}};
                  [Obsolete] public int F128 = alphaValue + betaValue + gammaValue + {{Long23}};
                  [Obsolete] public int F129 = alphaValue + betaValue + gammaValue + {{Long24}};
                  [Obsolete] public int {{Long14}} { get; set; }
                  [Obsolete] public int {{Long15}} { get; set; }
                  [Obsolete] public int {{Long25}} { get; set; }
                  [Obsolete] public int {{Long26}} { get; set; }
                  [Obsolete] public int {{Long27}} { get; set; }
                  [Obsolete] public int {{Long28}} { get; set; }
                  [Obsolete] public int {{Long29}} { get; set; }
                  [Obsolete] public int {{Long30}} { get; set; }
                  [Obsolete] public int {{Long31}} { get; set; }
                  [Obsolete] public int Q121 { get; set; } = alphaValue + {{Long32}};
                  [Obsolete] public int R121 => alphaValue + {{Long33}};
                  [Obsolete] public int Q123 { get; set; } = alphaValue + {{Long34}};
                  [Obsolete] public int R123 => alphaValue + {{Long35}};
                  [Obsolete] public int Q125 { get; set; } = alphaValue + {{Long36}};
                  [Obsolete] public int R125 => alphaValue + {{Long37}};
                  [Obsolete] public int Q127 { get; set; } = alphaValue + {{Long38}};
                  [Obsolete] public int R127 => alphaValue + {{Long39}};
              }
              """,
            $$"""
              public class In {
                  [Obsolete]
                  public int F121 = alphaValue + betaValue + gammaValue + {{Long16}};

                  [Obsolete] public int F122 =
                      alphaValue + betaValue + gammaValue + {{Long17}};

                  [Obsolete] public int F123 =
                      alphaValue + betaValue + gammaValue + {{Long18}};

                  [Obsolete] public int F124 =
                      alphaValue + betaValue + gammaValue + {{Long19}};

                  [Obsolete] public int F125 =
                      alphaValue + betaValue + gammaValue + {{Long20}};

                  [Obsolete] public int F126 =
                      alphaValue + betaValue + gammaValue + {{Long21}};

                  [Obsolete] public int F127 =
                      alphaValue + betaValue + gammaValue + {{Long22}};

                  [Obsolete] public int F128 =
                      alphaValue + betaValue + gammaValue + {{Long23}};

                  [Obsolete] public int F129 =
                      alphaValue + betaValue + gammaValue + {{Long24}};

                  [Obsolete] public int {{Long14}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long15}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long25}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long26}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long27}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long28}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long29}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long30}} {
                      get;
                      set;
                  }

                  [Obsolete] public int {{Long31}} {
                      get;
                      set;
                  }

                  [Obsolete] public int Q121 { get; set; } =
                      alphaValue + {{Long32}};

                  [Obsolete] public int R121 =>
                      alphaValue + {{Long33}};

                  [Obsolete] public int Q123 { get; set; } =
                      alphaValue + {{Long34}};

                  [Obsolete] public int R123 =>
                      alphaValue + {{Long35}};

                  [Obsolete] public int Q125 { get; set; } =
                      alphaValue + {{Long36}};

                  [Obsolete] public int R125 =>
                      alphaValue + {{Long37}};

                  [Obsolete] public int Q127 { get; set; } =
                      alphaValue + {{Long38}};

                  [Obsolete] public int R127 =>
                      alphaValue + {{Long39}};
              }
              """
        );

    /// <summary>
    ///     An abstract method's <c>;</c>, and an event field, which has nothing the oracle wraps inside
    ///     it and so declines the join at any overflow.
    /// </summary>
    [Fact]
    public void AbstractMethodsAndEvents() =>
        Agrees(
            $$"""
              public abstract class In {
                  {{Long41}}
                  {{Long42}}
                  {{Long43}}
                  {{Long44}}
                  [Obsolete] public event System.Action<{{Long40}}> EventNameXYZWVUTSR;
                  [Obsolete] public event System.Action<{{Long40}}> EventNameXYZWVUTSRQ;
                  [Obsolete] public event System.Action<{{Long40}}> EventNameXYZWVUTSRQP;
              }
              """,
            $$"""
              public abstract class In {
                  {{Long41}}
                  {{Long42}}
                  {{Long43}}

                  [Obsolete] public abstract void AbstractMethodName(
                      int alphaParameterValue,
                      int betaParameterValue,
                      int gammaValueXYZ
                  );

                  [Obsolete]
                  public event System.Action<{{Long40}}> EventNameXYZWVUTSR;

                  [Obsolete]
                  public event System.Action<{{Long40}}> EventNameXYZWVUTSRQ;

                  [Obsolete]
                  public event System.Action<{{Long40}}> EventNameXYZWVUTSRQP;
              }
              """
        );
}
