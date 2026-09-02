using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2163</c> — a duration is measured by subtracting two reads of the wall clock, which is not a
///     monotonic source.
/// </summary>
/// <remarks>
///     ⚠ <b>The wall clock can go backwards, and the two ways it does are both routine.</b> An NTP
///     correction moves it by whatever the drift was, in either direction, at a moment nobody chose; a
///     DST transition moves <c>DateTime.Now</c> by an hour twice a year. A duration measured across
///     either is wrong by that amount, and can be negative — so the guard that was supposed to catch a
///     slow operation reports a fast one, and a timeout computed from it fires immediately or never.
///     <c>Stopwatch</c> reads a monotonic counter that none of this touches.
///     <para>
///         ⚠ <b>Both ends must be the process's own clock reads, and that is what makes the rule
///         precise rather than noisy.</b> <c>DateTime.UtcNow - order.PlacedAt</c> is "how old is this
///         order", a legitimate question about wall-clock time that <c>Stopwatch</c> cannot answer at
///         all; only when the earlier value also came from this program reading the clock is the
///         subtraction a <em>measurement of elapsed time</em>. Requiring both ends is the difference
///         between a rule that fires on benchmarking code and one that fires on every date arithmetic in
///         a domain model.
///     </para>
///     <para>
///         ⚠ <b>The fix's preconditions are the rule's preconditions, deliberately.</b>
///         <c>hasFix: true</c> is a promise about every finding, not most of them, so a shape the fix
///         cannot repair is a shape this rule does not report: the start must be a local with one
///         declarator, assigned once, and read exactly once — by this subtraction. A start time carried
///         in a field or a property is the same defect and is <b>not</b> reported, because repairing it
///         means changing a declaration every other member can read and that is not an edit to one file's
///         text.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WallClockElapsedAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WallClockElapsed);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSubtraction, SyntaxKind.SubtractExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSubtractCall, SyntaxKind.InvocationExpression);
    }

    /// <summary>The operator spelling: <c>DateTime.UtcNow - start</c>.</summary>
    static void AnalyzeSubtraction(SyntaxNodeAnalysisContext context) {
        var subtraction = (BinaryExpressionSyntax)context.Node;
        Report(context, subtraction, subtraction.Left, subtraction.Right);
    }

    /// <summary>
    ///     The method spelling: <c>DateTime.UtcNow.Subtract(start)</c>, which is the same operation.
    /// </summary>
    static void AnalyzeSubtractCall(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax {
                Name.Identifier.ValueText: "Subtract"
            } access
            || invocation.ArgumentList.Arguments.Count != 1) {
            return;
        }

        Report(context, invocation, access.Expression, invocation.ArgumentList.Arguments[0].Expression);
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax whole,
        ExpressionSyntax later,
        ExpressionSyntax earlier
    ) {
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The later end must be the clock read written in place. `start - DateTime.UtcNow` is a
        // negative duration and a different mistake; reporting it here would attach this rule's
        // message and this rule's fix to code they do not describe.
        if (!Clock.IsStaticRead(model.GetOperation(later, cancellation), context.Compilation)) {
            return;
        }

        if (model.GetSymbolInfo(earlier, cancellation).Symbol is not ILocalSymbol local
            || !SymbolEqualityComparer.Default.Equals(local.Type, model.GetTypeInfo(later, cancellation).Type)
            || Clock.SingleAssignedInitializer(local, model, cancellation) is not { } initializer
            || !Clock.IsStaticRead(model.GetOperation(initializer, cancellation), context.Compilation)
            || Clock.ReferenceCount(local, model, cancellation) != 1
            || Declaration(initializer) is not { } declaration) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                whole.GetLocation(),
                FixEdits.Pack(Edits(declaration, initializer, local.Name, whole)),
                "elapsed time is measured by subtracting two reads of `"
                + Clock.NameOf(model.GetOperation(later, cancellation)!)
                + "`, which an NTP correction or a DST change can move backwards; use a `Stopwatch`"
            )
        );
    }

    /// <summary>
    ///     Turning the start local into a <c>Stopwatch</c>, and the subtraction into its
    ///     <c>Elapsed</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Stopwatch</c> is written fully qualified, so the fix needs no <c>using</c> the file may
    ///     not have. Both ends produce a <c>TimeSpan</c>, so whatever consumed the subtraction keeps
    ///     compiling unchanged. ⚠ The declared type is rewritten to <c>var</c> rather than to
    ///     <c>Stopwatch</c> for the same reason — <c>var</c> needs no import and cannot be spelled
    ///     wrongly.
    /// </remarks>
    static (TextSpan Span, string Text)[] Edits(
        VariableDeclarationSyntax declaration,
        ExpressionSyntax initializer,
        string name,
        ExpressionSyntax whole
    ) {
        var edits = ImmutableArray.CreateBuilder<(TextSpan, string)>();
        if (!declaration.Type.IsVar) {
            edits.Add((declaration.Type.Span, "var"));
        }

        edits.Add((initializer.Span, "System.Diagnostics.Stopwatch.StartNew()"));
        edits.Add((whole.Span, name + ".Elapsed"));
        return edits.ToArray();
    }

    /// <summary>
    ///     The declaration the fix rewrites, or <c>null</c> when it is not one the fix can rewrite.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>One declarator only.</b> <c>DateTime start = DateTime.UtcNow, deadline = …;</c> shares one
    ///     type node between two locals, so replacing it with <c>var</c> would retype the other one as a
    ///     <c>Stopwatch</c> too. ⚠ A local declared by a <c>foreach</c>, a <c>using</c>, a pattern or a
    ///     deconstruction has no rewritable initializer and is excluded by requiring the parent to be a
    ///     plain local declaration statement.
    /// </remarks>
    static VariableDeclarationSyntax? Declaration(ExpressionSyntax initializer) {
        if (initializer.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            || declarator.Parent is not VariableDeclarationSyntax { Variables.Count: 1 } declaration
            || declaration.Parent is not LocalDeclarationStatementSyntax { UsingKeyword.RawKind: 0 } statement
            || statement.Modifiers.Count != 0) {
            return null;
        }

        return declaration;
    }
}
