using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The seams between the seven equality rules, asserted as exact attribution.
/// </summary>
/// <remarks>
///     ⚠ <b>Disjointness that is only a property of the implementation is disjointness nobody will
///     notice losing.</b> <c>SK2004</c>, <c>SK2011</c> and <c>SK2040</c>–<c>SK2044</c> all look at the
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
    ///     ⚠ <c>SK2004</c>'s own shape. <c>SK2044</c> asks that rule's precondition before any of its
    ///     three, so the two cannot both report a type that is missing its object equality.
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
    ///     ⚠ The comparison site and the declaration are two rules' shapes and only one is a finding:
    ///     the type declares <c>operator ==</c>, so <c>SK2040</c>'s bound-operator test excludes the
    ///     comparison, and what is left is the missing <c>Equals(object)</c>.
    /// </summary>
    [Fact]
    public void AnOperatorWithoutObjectEquality_IsReportedAtTheDeclarationOnly() =>
        AssertExactly(
            """
            sealed class Money {
                public decimal Amount { get; init; }

                public static bool operator ==(Money? left, Money? right) => left?.Amount == right?.Amount;

                public static bool operator !=(Money? left, Money? right) => !(left == right);
            }

            class C {
                bool Same(Money left, Money right) => left == right;
            }
            """,
            RuleIds.InconsistentEqualityMembers
        );

    /// <summary>
    ///     ⚠ Two of <c>SK2044</c>'s three inconsistencies at once. One omission, one finding — the
    ///     sub-cases are ordered rather than accumulated.
    /// </summary>
    [Fact]
    public void TwoInconsistenciesInOneType_AreOneFinding() =>
        AssertExactly(
            """
            sealed class Key {
                public int Id { get; init; }

                public static bool operator ==(Key? left, Key? right) => left?.Id == right?.Id;

                public static bool operator !=(Key? left, Key? right) => !(left == right);

                public bool Equals(Key? other) => other is not null && other.Id == Id;
            }
            """,
            RuleIds.InconsistentEqualityMembers
        );

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
