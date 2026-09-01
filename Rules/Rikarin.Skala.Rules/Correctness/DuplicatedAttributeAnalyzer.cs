using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2103</c> — the same attribute is applied to one declaration twice with the same arguments.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>This is where the boundary against <c>SK2100</c>, <c>SK2101</c> and <c>SK2102</c> is
///         drawn, and it is drawn by construction rather than by a filter.</b> Those three ask one
///         question — <em>does this attribute contradict the declaration it is on?</em> — and to answer
///         it they read the declaration. This rule never reads the declaration at all. It asks a
///         question about a different pair of things: <em>do two applications of the same attribute say
///         the same thing?</em> Nothing this rule looks at is something they look at, so no shape can
///         reach both, and no exclusion list is needed to keep them apart.
///     </para>
///     <para>
///         Issue #269 names four sub-concepts and this is the one that is left. An attribute applied to
///         a target it does not declare is <c>CS0592</c>; applied twice when
///         <c>AllowMultiple</c> is false it is <c>CS0579</c>; naming a member that does not exist it is
///         <c>CS8776</c> for the nullable-contract attributes and <c>SK2102</c> for
///         <c>DebuggerDisplay</c>. ⚠ Repetition with identical arguments is the one the compiler is
///         silent about, because for an <c>AllowMultiple</c> attribute the repetition is legal — it is
///         simply indistinguishable from writing it once.
///     </para>
///     <para>
///         ⚠ Identity has to be <em>proved</em>, not assumed. If any argument on either application is
///         not a compile-time constant or a <c>typeof</c>, the pair is declined: two calls that might
///         differ are not a repetition, and this is the direction in which a wrong answer costs a false
///         positive on correct code.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicatedAttributeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DuplicatedAttribute);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AttributeList);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var list = (AttributeListSyntax)context.Node;
        if (list.Parent is not { } parent) {
            return;
        }

        // ⚠ `[method: X]` and `[return: X]` are applications to two different things that happen to be
        // written on one declaration, so the target is part of the group's identity.
        var target = list.Target?.Identifier.ValueText;
        var group = new List<AttributeListSyntax>();
        foreach (var sibling in AttributeContract.ListsOn(parent)) {
            if (string.Equals(sibling.Target?.Identifier.ValueText, target, StringComparison.Ordinal)) {
                group.Add(sibling);
            }
        }

        // The whole group is examined once, from its first list. Without this a declaration carrying
        // three lists would be walked three times and report the same repetition three times over.
        if (group.Count == 0 || group[0] != list) {
            return;
        }

        var seen = new List<(INamedTypeSymbol Class, AttributeSyntax Node)>();
        foreach (var current in group) {
            foreach (var attribute in current.Attributes) {
                var type = AttributeContract.Resolve(context.SemanticModel, attribute, context.CancellationToken);

                // ⚠ An attribute that does not allow multiples cannot legally repeat, so a second
                // application is CS0579 and this rule has nothing to add. Checking it also means the
                // rule stays silent on source that does not compile rather than piling on.
                if (type is null || !AttributeContract.AllowsMultiple(type)) {
                    continue;
                }

                var duplicate = false;
                foreach (var (earlierClass, earlier) in seen) {
                    if (SymbolEqualityComparer.Default.Equals(earlierClass, type)
                        && Same(context.SemanticModel, earlier, attribute, context.CancellationToken)) {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate) {
                    seen.Add((type, attribute));
                    continue;
                }

                var removal = AttributeContract.Removal(attribute);
                if (removal is null) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        attribute.GetLocation(),
                        FixEdits.Pack((removal.Value, string.Empty)),
                        "`"
                        + type.Name
                        + "` is applied again with the same arguments; nothing distinguishes this application from the first"
                    )
                );
            }
        }
    }

    /// <summary>Whether two applications of one attribute class provably say the same thing.</summary>
    static bool Same(
        SemanticModel model,
        AttributeSyntax left,
        AttributeSyntax right,
        CancellationToken cancellation
    ) {
        var a = left.ArgumentList?.Arguments ?? default;
        var b = right.ArgumentList?.Arguments ?? default;
        if (a.Count != b.Count) {
            return false;
        }

        var positionalA = new List<AttributeArgumentSyntax>();
        var positionalB = new List<AttributeArgumentSyntax>();
        var namedA = new Dictionary<string, AttributeArgumentSyntax>(StringComparer.Ordinal);
        var namedB = new Dictionary<string, AttributeArgumentSyntax>(StringComparer.Ordinal);

        if (!Split(a, positionalA, namedA) || !Split(b, positionalB, namedB)) {
            return false;
        }

        if (positionalA.Count != positionalB.Count || namedA.Count != namedB.Count) {
            return false;
        }

        for (var i = 0; i < positionalA.Count; i++) {
            if (!SameValue(model, positionalA[i].Expression, positionalB[i].Expression, cancellation)) {
                return false;
            }
        }

        // ⚠ No deconstruction: Rikarin.Skala.Rules targets netstandard2.0 so that it loads into `csc`
        // and into Rider, and `KeyValuePair<,>.Deconstruct` is not there.
        foreach (var entry in namedA) {
            if (!namedB.TryGetValue(entry.Key, out var other)
                || !SameValue(model, entry.Value.Expression, other.Expression, cancellation)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     ⚠ A <c>name:</c> argument makes position and name two different orderings of the same list,
    ///     so the pair is declined rather than guessed at. It is rare enough in an attribute that
    ///     handling it would be machinery nothing exercises.
    /// </summary>
    static bool Split(
        SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        List<AttributeArgumentSyntax> positional,
        Dictionary<string, AttributeArgumentSyntax> named
    ) {
        foreach (var argument in arguments) {
            if (argument.NameColon is not null) {
                return false;
            }

            if (argument.NameEquals is { } assignment) {
                var name = assignment.Name.Identifier.ValueText;
                if (named.ContainsKey(name)) {
                    return false;
                }

                named.Add(name, argument);
                continue;
            }

            positional.Add(argument);
        }

        return true;
    }

    static bool SameValue(
        SemanticModel model,
        ExpressionSyntax left,
        ExpressionSyntax right,
        CancellationToken cancellation
    ) {
        if (left is TypeOfExpressionSyntax leftType && right is TypeOfExpressionSyntax rightType) {
            var a = model.GetTypeInfo(leftType.Type, cancellation).Type;
            var b = model.GetTypeInfo(rightType.Type, cancellation).Type;
            return a is not null
                && b is not null
                && a.TypeKind != TypeKind.Error
                && b.TypeKind != TypeKind.Error
                && SymbolEqualityComparer.Default.Equals(a, b);
        }

        var one = model.GetConstantValue(left, cancellation);
        var two = model.GetConstantValue(right, cancellation);
        return one.HasValue && two.HasValue && Equals(one.Value, two.Value);
    }
}
