using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>csharp_preserve_single_line_blocks = false</c> (issue #510, SK-DIV-0162): the oracle expands
///     every one-statement accessor, lambda and anonymous-method body and every bodiless accessor list.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration with the key flipped alone, and again with both
///     <c>skala_keep_existing_*_block_arrangement</c> keys on — where the key is inert, because they
///     outrank it. The committed fixture is <c>Testing/corpus/constructs/braces/csharp_preserve_single_line_blocks.cs</c>.
/// </remarks>
public sealed class PreserveSingleLineBlocksIssue510Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          using System;
                          using System.Collections.Generic;
                          class C {
                              int _n;
                              int P { get { return _n; } }
                              int Q { get { return _n; } set { _n = value; } }
                              int R { get; set; }
                              int S { get => _n; }
                              void M() { A(); }
                              void Empty() { }
                              void N(bool c, object o) {
                                  Register(() => { A(); });
                                  Register(delegate { A(); });
                                  Action a = () => { A(); };
                                  Action b = delegate { A(); };
                                  Func<int, int> f = x => { return x; };
                                  if (c) { A(); }
                                  while (c) { A(); }
                                  void L() { A(); }
                                  var l = new List<int> { 1, 2 };
                                  var x = new { X = 1 };
                                  var s = _n switch { _ => 1 };
                                  var t = o is string { Length: 1 };
                                  if (c) { }
                                  Register(() => { });
                              }
                              void A() {
                              }
                              void Register(Action a) {
                              }
                          }
                          """;

    const string AtFalse = """
                           using System;
                           using System.Collections.Generic;

                           class C {
                               int _n;

                               int P {
                                   get {
                                       return _n;
                                   }
                               }

                               int Q {
                                   get {
                                       return _n;
                                   }
                                   set {
                                       _n = value;
                                   }
                               }

                               int R {
                                   get;
                                   set;
                               }

                               int S {
                                   get => _n;
                               }

                               void M() {
                                   A();
                               }

                               void Empty() { }

                               void N(bool c, object o) {
                                   Register(() => {
                                           A();
                                       }
                                   );
                                   Register(
                                       delegate {
                                           A();
                                       }
                                   );
                                   Action a = () => {
                                       A();
                                   };
                                   Action b = delegate {
                                       A();
                                   };
                                   Func<int, int> f = x => {
                                       return x;
                                   };
                                   if (c) {
                                       A();
                                   }

                                   while (c) {
                                       A();
                                   }

                                   void L() {
                                       A();
                                   }

                                   var l = new List<int> { 1, 2 };
                                   var x = new { X = 1 };
                                   var s = _n switch {
                                       _ => 1
                                   };
                                   var t = o is string { Length: 1 };
                                   if (c) { }

                                   Register(() => { });
                               }

                               void A() { }
                               void Register(Action a) { }
                           }
                           """;

    const string UnderTheKeepKeys = """
                                    using System;
                                    using System.Collections.Generic;

                                    class C {
                                        int _n;
                                        int P { get { return _n; } }
                                        int Q { get { return _n; } set { _n = value; } }
                                        int R { get; set; }
                                        int S { get => _n; }
                                        void M() { A(); }
                                        void Empty() { }

                                        void N(bool c, object o) {
                                            Register(() => { A(); });
                                            Register(delegate { A(); });
                                            Action a = () => { A(); };
                                            Action b = delegate { A(); };
                                            Func<int, int> f = x => { return x; };
                                            if (c) { A(); }

                                            while (c) { A(); }

                                            void L() { A(); }
                                            var l = new List<int> { 1, 2 };
                                            var x = new { X = 1 };
                                            var s = _n switch {
                                                _ => 1
                                            };
                                            var t = o is string { Length: 1 };
                                            if (c) { }

                                            Register(() => { });
                                        }

                                        void A() { }
                                        void Register(Action a) { }
                                    }
                                    """;

    /// <summary>
    ///     At <c>false</c> the accessor bodies, the bodiless accessor list, the lambdas and the anonymous
    ///     methods break open; the method, the local function and the <c>if</c> were already expanded at
    ///     <c>true</c>, and the initializers, the anonymous type, the property pattern and the empty blocks
    ///     stay joined at both values.
    /// </summary>
    [Fact]
    public void AtFalse_TheOracleExpandsEveryOneStatementAccessorLambdaAndAnonymousMethod() {
        var formatted = FormatWith(Source, ("csharp_preserve_single_line_blocks", "false"));
        Assert.Equal(AtFalse + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted, ("csharp_preserve_single_line_blocks", "false")));
    }

    /// <summary>
    ///     ⚠ Under both <c>keep_existing_*_block_arrangement</c> keys the oracle's answer is the same bytes
    ///     at both values: the keep keys outrank this one.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void UnderTheKeepKeys_TheKeyIsInert(string value) =>
        Assert.Equal(
            UnderTheKeepKeys + "\n",
            FormatWith(
                Source,
                ("csharp_preserve_single_line_blocks", value),
                ("skala_keep_existing_declaration_block_arrangement", "true"),
                ("skala_keep_existing_embedded_block_arrangement", "true")
            )
        );
}
