using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2231</c> — the command's SQL names a parameter the same method never supplies.
/// </summary>
/// <remarks>
///     <para>
///         The marker and the binding are two edits in two places and only one of them was made. It
///         is the same join a format string and its arguments make, against a different grammar, and
///         it fails the same way: the compiler sees a <c>string</c> on one side and a method call on
///         the other and has no reason to relate them.
///     </para>
///     <para>
///         ⚠ <b>The restrictions are the rule.</b> The command must be a <em>local</em>, must not
///         escape the method, must have its text assigned exactly once from a compile-time constant,
///         must have no <c>CommandType</c> assignment, and every use of <c>Parameters</c> must be a
///         recognised add with a constant name. One unrecognised use declines the whole method,
///         because after a single unknown name every remaining marker is unknowable.
///     </para>
///     <para>
///         ⚠ <b>At least one parameter must already have been added.</b> Zero is the shape where the
///         binding most plausibly happens somewhere this rule cannot see; #258's subject is getting
///         the count wrong rather than forgetting entirely.
///     </para>
///     <para>
///         ⚠ <b><c>SK5001</c> cannot overlap this.</b> It needs a tainted value reaching the SQL and
///         says nothing about a constant; this refuses to read a text that is not <em>entirely</em>
///         constant, so no string can satisfy both.
///     </para>
///     <para>
///         ⚠ <b>Nothing else reports it.</b> Probed outside this repository on SDK 10.0.400 at
///         <c>AnalysisMode=All</c>: a command missing a parameter, one supplying all of them, and one
///         supplying an extra produced identical diagnostic sets. <c>CA2100</c> is a constant-ness
///         rule and is silent on all three.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandParameterNotSuppliedAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CommandParameterNotSupplied);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var commands = new[] {
                    start.Compilation.GetTypeByMetadataName("System.Data.IDbCommand"),
                    start.Compilation.GetTypeByMetadataName("System.Data.Common.DbCommand")
                }.Where(static type => type is not null)
                    .Select(static type => type!)
                    .ToImmutableArray();

                if (commands.IsEmpty) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, commands),
                    SyntaxKind.SimpleAssignmentExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> commands) {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Left is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Name.Identifier.ValueText: "CommandText"
            } target) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(target, cancellation).Symbol is not IPropertySymbol { ContainingType: { } owner }
            || !IsACommand(owner, commands)) {
            return;
        }

        // ⚠ A field or a property is visible to every other method on the type, so what it has been
        // given is not a fact this method holds. A local is the only receiver whose whole life the
        // rule can see.
        if (model.GetSymbolInfo(target.Expression, cancellation).Symbol is not { } command) {
            return;
        }

        if (command.Kind != SymbolKind.Local) {
            return;
        }

        if (model.GetConstantValue(assignment.Right, cancellation) is not { HasValue: true, Value: string sql }) {
            return;
        }

        if (Body(assignment) is not { } body) {
            return;
        }

        var markers = MarkersIn(sql);
        if (markers.Count == 0) {
            return;
        }

        if (!Supplied(body, command, assignment, model, cancellation, out var supplied)) {
            return;
        }

        // ⚠ Zero adds is the shape where the binding most plausibly happens out of sight.
        if (supplied.Count == 0) {
            return;
        }

        var missing = markers.Where(marker => !supplied.Contains(marker)).ToList();
        if (missing.Count == 0) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                assignment.GetLocation(),
                "The SQL names "
                + string.Join(", ", missing.Select(static name => "`@" + name + "`"))
                + " and nothing in this method supplies "
                + (missing.Count == 1 ? "it" : "them")
            )
        );
    }

    static bool IsACommand(INamedTypeSymbol owner, ImmutableArray<INamedTypeSymbol> commands) =>
        commands.Any(command =>
            DerivesFrom(owner, command) || owner.AllInterfaces.Contains(command, SymbolEqualityComparer.Default)
        );

    static bool DerivesFrom(INamedTypeSymbol owner, INamedTypeSymbol command) {
        for (var current = (INamedTypeSymbol?)owner; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, command)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The whole member body the assignment sits in, or <c>null</c> where there is not one.</summary>
    /// <remarks>
    ///     ⚠ <b>A lambda is walked through, not stopped at.</b> Scoping to the lambda would read a
    ///     command declared outside it and miss every <c>Add</c> outside it, which reports a
    ///     parameter that is supplied — the one direction this rule must never get wrong. The
    ///     enclosing member is a superset of the scope in every case, and a superset only ever adds
    ///     supplied names and escape routes, both of which withdraw findings.
    /// </remarks>
    static SyntaxNode? Body(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return current;

                case GlobalStatementSyntax { Parent: { } unit }:
                    return unit;

                // ⚠ Not `and not BaseMethodDeclarationSyntax`: the case above already took those, so
                // the compiler calls the extra clause redundant (CS9335) and it is.
                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     The parameter names supplied to <paramref name="command" />, or <c>false</c> where the
    ///     method does anything with it the rule does not recognise.
    /// </summary>
    /// <remarks>
    ///     ⚠ The default answer is "decline". Every reference to the local is classified and anything
    ///     unrecognised — an argument, an alias, a return, an <c>AddRange</c>, a computed name —
    ///     abandons the method rather than being ignored, because a name the rule cannot read is a
    ///     name it must not claim is absent.
    /// </remarks>
    static bool Supplied(
        SyntaxNode body,
        ISymbol command,
        AssignmentExpressionSyntax textAssignment,
        SemanticModel model,
        System.Threading.CancellationToken cancellation,
        out HashSet<string> supplied
    ) {
        supplied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in body.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (!string.Equals(name.Identifier.ValueText, command.Name, StringComparison.Ordinal)
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(name, cancellation).Symbol,
                    command
                )) {
                continue;
            }

            if (!Classify(name, textAssignment, model, cancellation, supplied)) {
                return false;
            }
        }

        return true;
    }

    static bool Classify(
        IdentifierNameSyntax reference,
        AssignmentExpressionSyntax textAssignment,
        SemanticModel model,
        System.Threading.CancellationToken cancellation,
        HashSet<string> supplied
    ) {
        if (reference.Parent is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Expression != reference) {
            // The declarator that introduced the local, and a `using` header naming it, are the two
            // places the identifier stands on its own harmlessly. Everything else — an argument, an
            // alias, a return — hands the command to code this rule cannot read.
            return reference.Parent is VariableDeclaratorSyntax or UsingStatementSyntax;
        }

        switch (access.Name.Identifier.ValueText) {
            case "CommandText":
                // ⚠ Reading it is harmless. A *second* assignment means the text this rule read is
                // not the text that runs, so the method is abandoned rather than half-understood.
                return access.Parent is not AssignmentExpressionSyntax assigned
                    || assigned.Left != access
                    || assigned == textAssignment;

            case "CommandType":
                // A stored-procedure name is not SQL and its parameters are not in its text.
                return access.Parent is not AssignmentExpressionSyntax type || type.Left != access;

            case "Parameters":
                return Collect(access, model, cancellation, supplied);

            default:
                return true;
        }
    }

    /// <summary>
    ///     Reads one <c>command.Parameters.…</c> use, adding the name it supplies.
    /// </summary>
    static bool Collect(
        MemberAccessExpressionSyntax parameters,
        SemanticModel model,
        System.Threading.CancellationToken cancellation,
        HashSet<string> supplied
    ) {
        if (parameters.Parent is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } call
            || call.Parent is not InvocationExpressionSyntax invocation) {
            // `Parameters.Count`, an indexer read, a `foreach` over it — none of them add anything,
            // but none of them is recognisable enough to promise that either.
            return parameters.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Count" };
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0) {
            return false;
        }

        switch (call.Name.Identifier.ValueText) {
            case "Add" when arguments.Count == 1:
                // `Add(new SqlParameter("@id", value))` — the name is the creation's first argument.
                return arguments[0].Expression is BaseObjectCreationExpressionSyntax {
                    ArgumentList.Arguments.Count: > 0
                } creation
                    && Name(creation.ArgumentList!.Arguments[0].Expression, model, cancellation, supplied);

            case "Add":
            case "AddWithValue":
                return Name(arguments[0].Expression, model, cancellation, supplied);

            default:
                // `AddRange`, `Insert`, `RemoveAt`, `Clear` — every one of them changes the set in a
                // way the rule cannot read, so the method is abandoned.
                return false;
        }
    }

    static bool Name(
        ExpressionSyntax expression,
        SemanticModel model,
        System.Threading.CancellationToken cancellation,
        HashSet<string> supplied
    ) {
        if (model.GetConstantValue(expression, cancellation) is not { HasValue: true, Value: string name }) {
            return false;
        }

        supplied.Add(name.TrimStart('@'));
        return true;
    }

    /// <summary>
    ///     The parameter markers the SQL text really names.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>@@identity</c> and <c>@@rowcount</c> are T-SQL globals rather than parameters; an
    ///     <c>@</c> preceded by a word character is an address or part of a literal; and an <c>@</c>
    ///     inside a <c>'…'</c> string or either kind of SQL comment is text rather than a binding.
    ///     Every one of those would be reported as a missing parameter by a scan that only looked for
    ///     the sigil.
    /// </remarks>
    internal static List<string> MarkersIn(string sql) {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < sql.Length; i++) {
            var skipped = SkipNonBinding(sql, i);
            if (skipped != i) {
                i = skipped;
                continue;
            }

            if (sql[i] != '@' || (i > 0 && IsNameCharacter(sql[i - 1]))) {
                continue;
            }

            // ⚠ `@@identity` has to consume the whole global, not just the first sigil. Skipping
            // only the first `@` leaves the loop standing on the second one, whose predecessor is
            // `@` — not a name character — so it read `@identity` as a parameter and reported a
            // missing binding for a T-SQL global. Found by the fixture that exists for it.
            if (i + 1 < sql.Length && sql[i + 1] == '@') {
                i = EndOfName(sql, i + 2) - 1;
                continue;
            }

            var end = EndOfName(sql, i + 1);
            if (end > i + 1 && seen.Add(sql.Substring(i + 1, end - i - 1))) {
                found.Add(sql.Substring(i + 1, end - i - 1));
            }

            i = end - 1;
        }

        return found;
    }

    /// <summary>
    ///     The last index of the run starting at <paramref name="index" /> that cannot hold a binding
    ///     — a <c>'…'</c> literal or either kind of SQL comment — or <paramref name="index" /> itself
    ///     where the character starts none of them.
    /// </summary>
    static int SkipNonBinding(string sql, int index) {
        var c = sql[index];
        if (c == '\'') {
            return SkipQuoted(sql, index);
        }

        if (c == '-' && index + 1 < sql.Length && sql[index + 1] == '-') {
            var end = index;
            while (end < sql.Length && sql[end] != '\n') {
                end++;
            }

            return end;
        }

        if (c != '/' || index + 1 >= sql.Length || sql[index + 1] != '*') {
            return index;
        }

        var close = index + 2;
        while (close + 1 < sql.Length && !(sql[close] == '*' && sql[close + 1] == '/')) {
            close++;
        }

        return close + 1;
    }

    static int EndOfName(string sql, int start) {
        var end = start;
        while (end < sql.Length && IsNameCharacter(sql[end])) {
            end++;
        }

        return end;
    }

    /// <summary>The index of the closing apostrophe, treating <c>''</c> as an escaped one.</summary>
    static int SkipQuoted(string sql, int open) {
        for (var i = open + 1; i < sql.Length; i++) {
            if (sql[i] != '\'') {
                continue;
            }

            if (i + 1 < sql.Length && sql[i + 1] == '\'') {
                i++;
                continue;
            }

            return i;
        }

        return sql.Length;
    }

    static bool IsNameCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
}
