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
}
