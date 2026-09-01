using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2092</c> — <c>catch (NullReferenceException)</c>.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". A <c>NullReferenceException</c> is a
///     dereference that should not have happened, and naming it in a <c>catch</c> converts that bug into
///     a control-flow path the program is expected to take. The handler also covers a region far larger
///     than the one dereference that was meant, so the next null anywhere under the <c>try</c> is
///     silently absorbed by a recovery written for a different one.
///     <para>
///         ⚠ <b><c>catch (Exception)</c> catches it too and is deliberately not this rule.</b> A
///         catch-all is a different decision with a different argument — sometimes a correct one at a
///         process boundary — and folding the two together would make the finding unanswerable. This
///         rule fires only where <c>NullReferenceException</c> is the type the clause names, which is the
///         author stating that a null dereference is an expected outcome.
///     </para>
///     <para>
///         ⚠ <b>Syntactic, matching the name as written.</b> The three spellings a compiling program can
///         use are enumerated. The residual is a user-defined type whose own simple name is
///         <c>NullReferenceException</c>, or a <c>using</c> alias for the framework one; neither occurs on
///         either reference tree or in <c>Testing/corpus</c>, and buying them would cost the rule its
///         ability to run under <c>--load=loose</c>, where most of the code an agent writes is first seen.
///     </para>
///     <para>
///         Report-only. The repair is a null check at the dereference, which is somewhere under the
///         <c>try</c> and not at the <c>catch</c>; no edit here can write it, and deleting the clause
///         would turn a swallowed bug into a crash without anyone deciding that.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaughtNullReferenceAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullReferenceExceptionCaught);

    /// <summary>Every spelling of the one type this rule is about.</summary>
    static readonly HashSet<string> Spellings = new(StringComparer.Ordinal) {
        "NullReferenceException",
        "System.NullReferenceException",
        "global::System.NullReferenceException"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var clause = (CatchClauseSyntax)context.Node;

        // A bare `catch` names no type, so it is a catch-all and belongs to whatever rule covers those.
        if (clause.Declaration?.Type is not { } type || !Spellings.Contains(type.ToString())) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                type.GetLocation(),
                "catching `NullReferenceException` turns a dereference bug into an expected path, and "
                + "absorbs every other null under the `try` with it"
            )
        );
    }
}
