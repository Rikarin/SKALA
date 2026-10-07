using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     #397: what a single-file fixture cannot say about a C# 13/14 partial member.
/// </summary>
/// <remarks>
///     ⚠ A fixture asserts "fires" or "does not fire", and three of #397's findings were neither: a
///     member reported <em>twice</em>, once per half, is a positive fixture that passes; a fix that edits
///     one half is a finding that exists and a round trip that breaks; and a partial member split across
///     two files is a shape no fixture can hold. These say each of those directly.
/// </remarks>
public sealed class PartialMemberTests {
    static ImmutableArray<Diagnostic> Analyze(string first, string? second = null) {
        var compilation = RuleFixtures.Compile(first, "First.cs");
        if (second is not null) {
            compilation = compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    second,
                    (CSharpParseOptions)compilation.SyntaxTrees[0].Options,
                    "Second.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
        }

        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            "the probe does not compile: " + string.Join("; ", errors.Select(static e => e.ToString()))
        );

        var diagnostics = RuleFixtures.Analyze(compilation, SkalaAnalyzers.All, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
        return diagnostics;
    }

    static Diagnostic[] Of(ImmutableArray<Diagnostic> diagnostics, string id) =>
        diagnostics.Where(diagnostic => diagnostic.Id == id).ToArray();

    static int EditCount(Diagnostic diagnostic) =>
        diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var count)
            ? int.Parse(count!, System.Globalization.CultureInfo.InvariantCulture)
            : 0;

    /// <summary>
    ///     The issue's own shape, with the halves in two files, beside the canary that must still fire.
    /// </summary>
    [Fact]
    public void SK2131_DeclinesAPartialPropertyWhoseImplementationIsInAnotherFile() {
        var diagnostics = Analyze(
            """
            namespace Probe;

            public sealed partial class Loader {
                public partial int Count { get; }
            }

            public sealed class Plain {
                public int Count { get; }
            }
            """,
            """
            namespace Probe;

            public sealed partial class Loader {
                public partial int Count => 42;
            }
            """
        );

        var found = Of(diagnostics, "SK2131");
        Assert.Single(found);
        Assert.Equal("First.cs", found[0].Location.SourceTree!.FilePath);
        Assert.Equal(7, found[0].Location.GetLineSpan().StartLinePosition.Line);
    }

    /// <summary>One member, one finding — on the definition, for the kinds a generator implements.</summary>
    [Fact]
    public void AnUndocumentedPartialMember_IsReportedOnceAndOnItsDefinition() {
        var diagnostics = Analyze(
            """
            namespace Probe;

            /// <summary>Public.</summary>
            public sealed partial class Open {
                public partial int Count { get; }

                public partial int Count => 4;
            }

            /// <summary>Internal.</summary>
            internal sealed partial class Closed {
                internal partial int Count { get; }

                internal partial int Count => 4;
            }
            """
        );

        var open = Of(diagnostics, "SK7010")
            .Where(static d => d.GetMessage().Contains("`Count`", StringComparison.Ordinal))
            .ToArray();
        var closed = Of(diagnostics, "SK7101")
            .Where(static d => d.GetMessage().Contains("`Count`", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(open);
        Assert.Single(closed);
        Assert.Equal(4, open[0].Location.GetLineSpan().StartLinePosition.Line);
        Assert.Equal(11, closed[0].Location.GetLineSpan().StartLinePosition.Line);
    }

    /// <summary>
    ///     ⚠ The constructor is the exception, and the reason is measured rather than chosen: no
    ///     syntax-node action ever visits a partial constructor's definition, so the implementation
    ///     carries its findings or nothing does.
    /// </summary>
    [Fact]
    public void AnUndocumentedPartialConstructor_IsReportedOnceOnItsImplementation() {
        var diagnostics = Analyze(
            """
            namespace Probe;

            /// <summary>Public.</summary>
            public sealed partial class Open {
                public partial Open(int seed);

                public partial Open(int seed) { }
            }
            """
        );

        var found = Of(diagnostics, "SK7010")
            .Where(static d => d.Location.GetLineSpan().StartLinePosition.Line > 3)
            .ToArray();
        Assert.Single(found);
        Assert.Equal(6, found[0].Location.GetLineSpan().StartLinePosition.Line);
    }

    /// <summary>
    ///     An event's definition is spelled as a field-like event, which is not measured: the accessors
    ///     carry it.
    /// </summary>
    [Fact]
    public void AnUndocumentedPartialEvent_IsReportedOnceOnItsImplementation() {
        var diagnostics = Analyze(
            """
            namespace Probe;

            /// <summary>Public.</summary>
            public sealed partial class Open {
                public partial event System.EventHandler? Changed;

                public partial event System.EventHandler? Changed { add { } remove { } }
            }
            """
        );

        var found = Of(diagnostics, "SK7010")
            .Where(static d => d.GetMessage().Contains("`Changed`", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(found);
        Assert.Equal(6, found[0].Location.GetLineSpan().StartLinePosition.Line);
    }

    /// <summary>
    ///     ⚠ <c>field</c> binds to the compiler's backing field, which is the property's own storage: the
    ///     finding names the property, not <c>&lt;Customer&gt;k__BackingField</c>.
    /// </summary>
    [Fact]
    public void AHashCodeOverAFieldBackedPartialProperty_NamesTheProperty() {
        var diagnostics = Analyze(
            """
            sealed partial class Order {
                public Order(int id, string customer) {
                    Id = id;
                    Customer = customer;
                }

                public int Id { get; }

                public partial string Customer { get; }

                public partial string Customer {
                    get => field;
                }

                public override bool Equals(object? other) => other is Order order && order.Id == Id;

                public override int GetHashCode() => System.HashCode.Combine(Id, Customer);
            }
            """
        );

        Assert.Contains("`Customer`", Assert.Single(Of(diagnostics, "SK2042")).GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void APartialMemberOverTheParameterThreshold_IsReportedOnce() {
        var diagnostics = Analyze(
            """
            namespace Probe;

            public sealed partial class Wide {
                public partial Wide(int a, int b, int c, int d, int e, int f, int g, int h, int i);

                public partial Wide(int a, int b, int c, int d, int e, int f, int g, int h, int i) { }

                public partial void Run(int a, int b, int c, int d, int e, int f, int g, int h, int i);

                public partial void Run(int a, int b, int c, int d, int e, int f, int g, int h, int i) { }
            }
            """
        );

        Assert.Equal(2, Of(diagnostics, "SK7005").Length);
    }

    /// <summary>Both halves edited, or no finding: never one half.</summary>
    [Fact]
    public void AnAbstractTypesPartialConstructor_IsFixedOnBothHalves() {
        var diagnostics = Analyze(
            """
            public abstract partial class Importer {
                public partial Importer(string name);

                public partial Importer(string name) { }
            }
            """
        );

        Assert.Equal(2, EditCount(Assert.Single(Of(diagnostics, "SK6003"))));
    }

    /// <summary>
    ///     ⚠ The definition in another file cannot be edited from this one, and editing the implementation
    ///     alone is <c>CS8799</c>: declining is the only finding that leaves the build working.
    /// </summary>
    [Fact]
    public void AnAbstractTypesPartialConstructor_SplitAcrossFiles_IsDeclined() {
        var diagnostics = Analyze(
            """
            public abstract partial class Importer {
                public partial Importer(string name);
            }
            """,
            """
            public abstract partial class Importer {
                public partial Importer(string name) { }
            }
            """
        );

        Assert.Empty(Of(diagnostics, "SK6003"));
    }

    const string Logging = """
                           namespace Microsoft.Extensions.Logging {
                               interface ILogger { }

                               interface ILogger<out TCategoryName> : ILogger { }
                           }
                           """;

    [Fact]
    public void ALoggerOnAPartialConstructor_IsFixedOnBothHalves() {
        var diagnostics = Analyze(
            Logging
            + """

              namespace Fixtures {
                  using Microsoft.Extensions.Logging;

                  sealed class PaymentService { }

                  sealed partial class OrderService {
                      public partial OrderService(ILogger<PaymentService> logger);

                      public partial OrderService(ILogger<PaymentService> logger) { }
                  }
              }
              """
        );

        Assert.Equal(2, EditCount(Assert.Single(Of(diagnostics, "SK7110"))));
    }

    [Fact]
    public void ALoggerOnAPartialConstructor_SplitAcrossFiles_IsDeclined() {
        var diagnostics = Analyze(
            Logging
            + """

              namespace Fixtures {
                  using Microsoft.Extensions.Logging;

                  sealed class PaymentService { }

                  sealed partial class OrderService {
                      public partial OrderService(ILogger<PaymentService> logger);
                  }
              }
              """,
            """
            namespace Fixtures {
                using Microsoft.Extensions.Logging;

                sealed partial class OrderService {
                    public partial OrderService(ILogger<PaymentService> logger) { }
                }
            }
            """
        );

        Assert.Empty(Of(diagnostics, "SK7110"));
    }

    /// <summary>
    ///     ⚠ The fixture fires either way; only the message tells the two reasons apart, and the event
    ///     case used to give the wrong one.
    /// </summary>
    [Fact]
    public void AnInheritdocOnAPartialEventImplementation_SaysItMasksTheDefinition() {
        var diagnostics = Analyze(
            File.ReadAllText(
                Path.Combine(
                    RuleFixtures.Root,
                    "SK7103",
                    "positive",
                    "a-partial-event-implementation-masking-the-definition.cs"
                )
            )
        );

        Assert.Contains(
            "replaces the definition's",
            Assert.Single(Of(diagnostics, "SK7103")).GetMessage(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     Applies every edit of every <paramref name="id" /> finding to <paramref name="source" /> and
    ///     asserts the result compiles — the round trip a fix on one half of a partial member fails.
    /// </summary>
    static void AssertTheFixCompiles(string source, string id) {
        var found = Of(Analyze(source), id);
        Assert.NotEmpty(found);
        var edits = found.SelectMany(static diagnostic => Enumerable.Range(0, EditCount(diagnostic))
                .Select(index => (
                    Start: Number(diagnostic, FixEdits.StartKey(index)),
                    Length: Number(diagnostic, FixEdits.LengthKey(index)),
                    Text: diagnostic.Properties[FixEdits.TextKey(index)] ?? string.Empty
                )))
            .OrderByDescending(static edit => edit.Start)
            .ToArray();
        Assert.NotEmpty(edits);
        var text = source;
        foreach (var (start, length, replacement) in edits) {
            text = text[..start] + replacement + text[(start + length)..];
        }

        var errors = RuleFixtures.Compile(text, "Fixed.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            "the fix does not compile: " + string.Join("; ", errors.Select(static e => e.ToString())) + "\n" + text
        );
    }

    static int Number(Diagnostic diagnostic, string key) =>
        int.Parse(diagnostic.Properties[key]!, System.Globalization.CultureInfo.InvariantCulture);

    static string Fixture(string id, string kind, string name) =>
        File.ReadAllText(Path.Combine(RuleFixtures.Root, id, kind, name));

    /// <summary>#400: one partial method, one finding — on the definition, in whichever file it is.</summary>
    [Fact]
    public void SK6053_APartialMethod_IsReportedOnceOnItsDefinition() {
        var together = Of(
            Analyze(Fixture("SK6053", "positive", "a-partial-method-reported-on-its-definition.cs")),
            "SK6053"
        );
        Assert.Equal(7, Assert.Single(together).Location.GetLineSpan().StartLinePosition.Line);

        var apart = Of(
            Analyze(
                """
                using System.Threading.Tasks;

                public sealed partial class Store {
                    public partial Task<int> Fetch(int id);
                }
                """,
                """
                using System.Threading.Tasks;

                public sealed partial class Store {
                    public partial Task<int> Fetch(int id) => Task.FromResult(id);
                }
                """
            ),
            "SK6053"
        );
        Assert.Equal("First.cs", Assert.Single(apart).Location.SourceTree!.FilePath);
    }

    /// <summary>
    ///     ⚠ A pin for a refuted #400 claim: a definition beside an <c>async</c> implementation was never
    ///     reported as falsely named, in one file or two. It stays green with the fix reverted, on purpose.
    /// </summary>
    [Fact]
    public void SK6053_APartialMethodAsyncInAnotherFile_IsNotNamedFalsely() {
        var diagnostics = Analyze(
            """
            public sealed partial class Panel {
                public partial void RefreshAsync();
            }
            """,
            """
            using System.Threading.Tasks;

            public sealed partial class Panel {
                public async partial void RefreshAsync() {
                    await Task.Yield();
                }
            }
            """
        );

        Assert.Empty(Of(diagnostics, "SK6053"));
    }

    const string SuiteBodies = """
                               public partial void Blocks() {
                                   Console.WriteLine(File.ReadAllTextAsync("x").Result.Length);
                               }

                               public async partial void Throws() {
                                   await Task.Yield();
                                   throw new InvalidOperationException("expected");
                               }

                               public partial void Derives() {
                                   var key = Rfc2898DeriveBytes.Pbkdf2(
                                       "p",
                                       Encoding.UTF8.GetBytes("salt"),
                                       1,
                                       HashAlgorithmName.SHA1,
                                       20
                                   );
                                   Console.WriteLine(key.Length);
                               }

                               public partial void Opens() {
                                   File.SetUnixFileMode("x", UnixFileMode.UserRead | UnixFileMode.OtherWrite);
                               }
                               """;

    const string SuiteUsings = """
                               using System;
                               using System.IO;
                               using System.Security.Cryptography;
                               using System.Text;
                               using System.Threading.Tasks;

                               """;

    static readonly string[] TestExempted = ["SK3002", "SK3050", "SK5041", "SK5042"];

    /// <summary>
    ///     #400: a test attribute on a partial method's definition exempts the implementation's body,
    ///     with the two halves in two files — where only the symbol can see across.
    /// </summary>
    [Fact]
    public void TheTestMethodExemption_ReadsAPartialDefinitionInAnotherFile() {
        var diagnostics = Analyze(
            """
            public sealed class FactAttribute : System.Attribute { }

            public sealed partial class Suite {
                [Fact]
                public partial void Blocks();

                [Fact]
                public partial void Throws();

                [Fact]
                public partial void Derives();

                [Fact]
                public partial void Opens();
            }
            """,
            SuiteUsings + "public sealed partial class Suite {\n" + SuiteBodies + "\n}\n"
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => TestExempted.Contains(diagnostic.Id));
    }

    /// <summary>The canary for the test above: the same bodies with no attribute anywhere are reported.</summary>
    [Fact]
    public void TheTestMethodExemption_Canary_FiresWithoutTheAttribute() {
        var diagnostics = Analyze(
            """
            public sealed partial class Suite {
                public partial void Blocks();

                public partial void Throws();

                public partial void Derives();

                public partial void Opens();
            }
            """,
            SuiteUsings + "public sealed partial class Suite {\n" + SuiteBodies + "\n}\n"
        );

        Assert.Equal(TestExempted, TestExempted.Where(id => Of(diagnostics, id).Length > 0));
    }

    /// <summary>#400: a redundant partial override is deleted whole, or not at all.</summary>
    [Fact]
    public void SK0244_ARedundantPartialOverride_IsDeletedOnBothHalves() {
        var source = Fixture("SK0244", "positive", "a-partial-override-forwarding-to-base.cs");
        Assert.Equal(2, EditCount(Assert.Single(Of(Analyze(source), "SK0244"))));
        AssertTheFixCompiles(source, "SK0244");
    }

    [Fact]
    public void SK0244_ARedundantPartialOverride_SplitAcrossFiles_IsDeclined() {
        var diagnostics = Analyze(
            """
            class Base {
                public virtual void Flush() { }
            }

            partial class Writer : Base {
                public override partial void Flush();
            }
            """,
            """
            partial class Writer {
                public override partial void Flush() => base.Flush();
            }
            """
        );

        Assert.Empty(Of(diagnostics, "SK0244"));
    }

    /// <summary>#400: the <c>params</c> disagreement is fixed on both halves, and the round trip compiles.</summary>
    [Fact]
    public void SK2140_APartialMethodsParams_IsFixedOnBothHalves() {
        var source = Fixture("SK2140", "positive", "a-partial-interface-implementation-drops-params.cs");
        Assert.Equal(2, EditCount(Assert.Single(Of(Analyze(source), "SK2140"))));
        AssertTheFixCompiles(source, "SK2140");
    }

    /// <summary>
    ///     ⚠ #400: the definition's defaults are the method's, wherever the implementation is — and a
    ///     <c>params</c> edit that cannot reach the implementation's file is declined rather than made
    ///     on one half.
    /// </summary>
    [Fact]
    public void SK2140_APartialMethodSplitAcrossFiles_ReadsTheDefinitionAndDeclinesAOneHalfFix() {
        var diagnostics = Analyze(
            """
            namespace Fixtures {
                abstract class Writer {
                    public virtual void Write(string text, bool flush = false) { }
                }

                interface IPlain {
                    void Accept(string name, params int[] values);
                }

                sealed partial class BufferedWriter : Writer, IPlain {
                    public override partial void Write(string text, bool flush = false);

                    public partial void Accept(string name, int[] values);
                }
            }
            """,
            """
            namespace Fixtures {
                sealed partial class BufferedWriter {
                    public override partial void Write(string text, bool flush) { }

                    public partial void Accept(string name, int[] values) { }
                }
            }
            """
        );

        Assert.Empty(Of(diagnostics, "SK2140"));
    }

    /// <summary>#400: a call under the lock binds to the definition; the writes are the implementation's.</summary>
    [Fact]
    public void SK3044_APartialMethodCalledUnderTheLock_InAnotherFile_IsGuarded() {
        var diagnostics = Analyze(
            """
            public sealed partial class Registry {
                readonly object gate = new();

                int count;

                public void Add() {
                    lock (gate) {
                        Reset();
                    }
                }

                public int Read() {
                    lock (gate) {
                        return count;
                    }
                }

                public void Bump() {
                    lock (gate) {
                        count++;
                    }
                }

                public partial void Reset();
            }
            """,
            """
            public sealed partial class Registry {
                public partial void Reset() => count = 0;
            }
            """
        );

        Assert.Empty(Of(diagnostics, "SK3044"));
    }

    const string DefinitionBase = """
                                  using System;
                                  using System.Diagnostics.CodeAnalysis;

                                  /// <summary>A base.</summary>
                                  public abstract class Shape {
                                      /// <summary>Creates it.</summary>
                                      protected Shape() { }
                                  }

                                  """;

    const string Definition = """
                              /// <summary>A circle.</summary>
                              public sealed partial class Circle : Shape {
                                  /// <inheritdoc />
                                  /// <returns>A circle.</returns>
                                  [Obsolete]
                                  [SuppressMessage("Category", "Id")]
                                  [ExcludeFromCodeCoverage]
                                  [SetsRequiredMembers]
                                  public partial Circle(Nullable<int> radius, System.String name);
                              }

                              """;

    const string Implementation = """
                                  public sealed partial class Circle {
                                      public partial Circle(int? radius, string name) { }
                                  }
                                  """;

    /// <summary>
    ///     #401: everything the driver dropped on a partial constructor definition, by the rules the
    ///     mini-driver measured — each reported exactly once, and on the definition.
    /// </summary>
    static readonly string[] DefinitionRules = [
        "SK7102", "SK7103", "SK7070", "SK7051", "SK7071", "SK0281", "SK1040", "SK0243"
    ];

    [Fact]
    public void APartialConstructorDefinition_IsReadByEveryRule_Once() {
        var source = DefinitionBase + Definition + Implementation;
        var diagnostics = Analyze(source);
        var definitionLine = source.Split('\n').ToList().FindIndex(static line => line.Contains("Nullable<int>"));
        foreach (var id in DefinitionRules) {
            var found = Of(diagnostics, id);
            Assert.True(found.Length == 1, id + " reported " + found.Length + " times");
            var line = found[0].Location.GetLineSpan().StartLinePosition.Line;
            Assert.InRange(line, definitionLine - 7, definitionLine);
        }
    }

    /// <summary>The same with the definition in a file of its own, which a semantic-model action of its tree reaches.</summary>
    [Fact]
    public void APartialConstructorDefinition_InAFileOfItsOwn_IsReadByEveryRule_Once() {
        var diagnostics = Analyze(DefinitionBase + Definition, Implementation);
        foreach (var id in DefinitionRules) {
            var found = Of(diagnostics, id);
            Assert.True(found.Length == 1, id + " reported " + found.Length + " times");
            Assert.Equal("First.cs", found[0].Location.SourceTree!.FilePath);
        }
    }

    /// <summary>Every fix made on the definition alone leaves the pair compiling: none of them changes the signature.</summary>
    [Fact]
    public void APartialConstructorDefinition_FixesCompile() {
        var source = DefinitionBase + Definition + Implementation;
        foreach (var id in new[] { "SK7102", "SK0281", "SK1040", "SK0243" }) {
            AssertTheFixCompiles(source, id);
        }
    }
}
