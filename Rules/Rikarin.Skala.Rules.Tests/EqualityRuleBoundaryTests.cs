using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The seams between the seven equality rules, asserted as exact attribution.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         Disjointness that is only a property of the implementation is disjointness nobody will
///         notice losing.
///     </b> <c>SK2004</c>, <c>SK2011</c> and <c>SK2040</c>–<c>SK2044</c> all look at the
///     same handful of members, and every one of the shapes below is one a reasonable reading would
///     hand to two of them. Each is pinned to the single rule that owns it, so a later widening that
///     makes two rules argue over one span fails here rather than in somebody's report — where it
///     reads as the tool being noisy rather than as two rules overlapping.
///     <para>
///         The claim is scoped to these seven analyzers on purpose: it is about which equality rule
///         owns a shape, not about what the rest of the catalogue says on the same file.
///     </para>
/// </remarks>
public sealed class EqualityRuleBoundaryTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Equality = [
        new IncompleteEqualityContractAnalyzer(),
        new InheritedValueTypeEqualsAnalyzer(),
        new UnintendedReferenceComparisonAnalyzer(),
        new BaseEqualityCallAnalyzer(),
        new UncomparedHashMemberAnalyzer(),
        new MutableHashMemberAnalyzer(),
        new InconsistentEqualityMembersAnalyzer(),
    ];

    /// <summary>
    ///     ⚠ Mutable <em>and</em> uncompared. A filter would let both hash rules see this; the
    ///     partition in <c>HashCodeContract</c> puts it in exactly one half.
    /// </summary>
    [Fact]
    public void AMutableMemberEqualityIgnores_IsReportedOnlyAsAContractBreak() =>
        AssertExactly(
            """
            using System;

            sealed class Ticket {
                public int Id { get; init; }

                public string Holder { get; set; } = "";

                public override bool Equals(object? other) => other is Ticket ticket && ticket.Id == Id;

                public override int GetHashCode() => HashCode.Combine(Id, Holder);
            }
            """,
            RuleIds.HashCodeOverUncomparedMember
        );

    /// <summary>The same member, once equality does compare it: mutability becomes the only question.</summary>
    [Fact]
    public void AMutableMemberEqualityCompares_IsReportedOnlyAsMutableState() =>
        AssertExactly(
            """
            using System;

            sealed class Ticket {
                public int Id { get; init; }

                public string Holder { get; set; } = "";

                public override bool Equals(object? other) =>
                    other is Ticket ticket && ticket.Id == Id && ticket.Holder == Holder;

                public override int GetHashCode() => HashCode.Combine(Id, Holder);
            }
            """,
            RuleIds.MutableHashCodeMember
        );

    /// <summary>
    ///     ⚠ <c>SK2004</c>'s own shape. <c>SK2044</c> asks that rule's precondition before either of
    ///     its two, so the two cannot both report a type that is missing its object equality.
    /// </summary>
    [Fact]
    public void ATypedContractWithNoObjectEquality_IsLeftToTheOlderRule() =>
        AssertExactly(
            """
            using System;

            sealed class Handle : IEquatable<Handle> {
                public int Id { get; init; }

                public bool Equals(Handle? other) => other is not null && other.Id == Id;
            }
            """,
            RuleIds.IncompleteEqualityContract
        );

    /// <summary>
    ///     ⚠ The shape the gate above actually holds off — and the neighbouring test does not reach
    ///     it.
    /// </summary>
    /// <remarks>
    ///     <c>SK2044</c>'s typed-<c>Equals</c> half is mutually exclusive with <c>SK2004</c> by its
    ///     own condition, so <see cref="ATypedContractWithNoObjectEquality_IsLeftToTheOlderRule" />
    ///     passes whether or not the gate exists. Only the ordering half can reach a type
    ///     <c>SK2004</c> also reports. A sabotage removing the gate turned nothing red until this
    ///     case was written, which is the whole argument for sabotaging.
    /// </remarks>
    [Fact]
    public void AnEquatableOrderingWithNoObjectEquality_IsStillLeftToTheOlderRule() =>
        AssertExactly(
            """
            using System;

            sealed class Revision : IComparable<Revision>, IEquatable<Revision> {
                public int Number { get; init; }

                public static bool operator ==(Revision? left, Revision? right) => Equals(left, right);

                public static bool operator !=(Revision? left, Revision? right) => !(left == right);

                public int CompareTo(Revision? other) => other is null ? 1 : Number.CompareTo(other.Number);

                public bool Equals(Revision? other) => other is not null && other.Number == Number;
            }
            """,
            RuleIds.IncompleteEqualityContract
        );

    /// <summary>
    ///     A hash that reads nothing but <c>base</c> is one finding, not three: the delegation is
    ///     <c>SK2041</c>'s, and it leaves both hash-member sets empty rather than disagreeing.
    /// </summary>
    [Fact]
    public void AHashThatDelegatesToObject_IsReportedOnlyAsTheDelegation() =>
        AssertExactly(
            """
            sealed class Key {
                public string Name { get; set; } = "";

                public override bool Equals(object? other) => other is Key key && key.Name == Name;

                public override int GetHashCode() => base.GetHashCode();
            }
            """,
            RuleIds.BaseEqualityCallIsIdentity
        );

    /// <summary>
    ///     ⚠ Both of <c>SK2044</c>'s inconsistencies at once. One omission, one finding — the halves
    ///     are ordered rather than accumulated.
    /// </summary>
    [Fact]
    public void TwoInconsistenciesInOneType_AreOneFinding() =>
        AssertExactly(
            """
            using System;

            sealed class Revision : IComparable<Revision> {
                public int Number { get; init; }

                public static bool operator ==(Revision? left, Revision? right) => Equals(left, right);

                public static bool operator !=(Revision? left, Revision? right) => !(left == right);

                public int CompareTo(Revision? other) => other is null ? 1 : Number.CompareTo(other.Number);

                public bool Equals(Revision? other) => other is not null && other.Number == Number;

                public override bool Equals(object? other) => other is Revision revision && Equals(revision);

                public override int GetHashCode() => Number;
            }
            """,
            RuleIds.InconsistentEqualityMembers
        );

    /// <summary>
    ///     ⚠ A base list that did not bind withdraws the type from every rule in the batch.
    /// </summary>
    /// <remarks>
    ///     This one is not a fixture, because a fixture has to compile and the whole point is a
    ///     compilation where a name did not resolve. It was found by measurement rather than by
    ///     reading: <c>Vixen.Raven</c>'s <c>BufferTypeSymbol</c> declares
    ///     <c>IEquatable&lt;BufferTypeSymbol&gt;</c>, and without the SDK's implicit global usings
    ///     that name binds to an error type — so <c>AllInterfaces</c> held <c>IEquatable&lt;&gt;</c>,
    ///     the comparison against <c>System.IEquatable`1</c> failed, and <c>SK2044</c> reported the
    ///     type for not implementing the interface it implements. Skala loads compilations three
    ///     ways and two of them can be incomplete, so this is a live shape rather than a lab one.
    /// </remarks>
    [Fact]
    public void AnInterfaceThatDidNotBind_WithdrawsTheTypeEntirely() {
        // No `using System;`, so `IEquatable` is an error type — deliberately.
        const string source = """
                              public sealed class Handle : IEquatable<Handle> {
                                  public int Id { get; init; }

                                  public bool Equals(Handle? other) => other is not null && other.Id == Id;

                                  public override bool Equals(object? other) => Equals(other as Handle);

                                  public override int GetHashCode() => Id;
                              }
                              """;

        var compilation = RuleFixtures.Compile(source, "unbound.cs");
        Assert.Contains(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error && d.Id == "CS0246"
        );

        var produced = RuleFixtures.Analyze(compilation, Equality, TestContext.Current.CancellationToken);
        Assert.True(
            produced.Length == 0,
            "a type whose base list did not bind was reported anyway: "
            + string.Join(", ", produced.Select(static d => d.Id + ": " + d.GetMessage()))
        );
    }

    static void AssertExactly(string source, string expected) {
        var compilation = RuleFixtures.Compile(source, "boundary.cs");
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            "the boundary source does not compile, so it proves nothing: "
            + string.Join("; ", errors.Take(3).Select(static d => d.ToString()))
        );

        var produced = RuleFixtures.Analyze(compilation, Equality, TestContext.Current.CancellationToken)
            .Select(static diagnostic => diagnostic.Id + " at " + diagnostic.Location.GetLineSpan().StartLinePosition)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            produced.Length == 1 && produced[0].StartsWith(expected, StringComparison.Ordinal),
            $"expected exactly one {expected}, got: {string.Join(", ", produced)}"
        );
    }
}
