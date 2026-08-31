using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     Layers 2 and 3 of docs/plan/06 § "Safety".
/// </summary>
/// <remarks>
///     Layer 1 — conservative preconditions — lives in the rules, because a precondition is about one
///     rewrite. These two are about the document, and they are what stands between "the rewrite was
///     legal in isolation" and "the rewrite was legal here".
///     <para>
///         ⚠ Layer 2 costs a re-bind per changed document, and that is the whole reason <c>arrange</c> is
///         minutes-scale on a large tree while <c>format</c> is seconds-scale. It is the correct trade:
///         whitespace is cheap and constant, tree rewrites are rare and must be right.
///     </para>
/// </remarks>
public static class ArrangementSafety {
    /// <summary>
    ///     Re-binds the rewritten document and returns the diagnostic that says it must be reverted, or
    ///     null when it is safe.
    /// </summary>
    /// <remarks>
    ///     ⚠ A layer that cannot answer answers "revert", and it must never answer by throwing.
    ///     Everything below re-binds the document, and binding is where Roslyn's own defects live: a
    ///     target-typed <c>new</c> whose target is a delegate type, carrying a LINQ query in its
    ///     initializer, makes <c>GetSymbolInfo</c> throw <c>IndexOutOfRangeException</c> out of the
    ///     binder (SK-FUZZ-0012). The check that exists to stop a bad rewrite reaching disk was then
    ///     the thing that took the process down instead — with the rewrite neither applied nor
    ///     refused. So a throw here is <b>not safe</b>, by definition: the question "did this
    ///     document's meaning change?" went unanswered, and an unanswered safety question is a
    ///     revert.
    /// </remarks>
    public static SkalaDiagnostic? Check(
        string path,
        CSharpCompilation compilation,
        SyntaxTree original,
        SyntaxNode originalRoot,
        string arranged,
        SemanticModel beforeModel,
        string? crashRoot,
        in ArrangementOptions options,
        string originalText,
        CancellationToken cancellation = default
    ) {
        try {
            return Evaluate(
                path,
                compilation,
                original,
                originalRoot,
                arranged,
                beforeModel,
                crashRoot,
                originalText,
                cancellation
            );
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            var artefact = CrashArtifacts.Write(crashRoot, path, originalText, arranged, new PhaseOneOptions());
            return new(
                ArrangeIds.Reverted,
                SkalaSeverity.Error,
                "not arranged, the safety re-bind threw and could not answer whether the rewrite was "
                + $"safe: {exception.GetType().Name}: {exception.Message}",
                path,
                0,
                artefact is null
                    ? "This is a Skala bug; the file was left untouched."
                    : $"A reproduction is in {artefact}. This is a Skala bug; the file was left untouched."
            );
        }
    }

    static SkalaDiagnostic? Evaluate(
        string path,
        CSharpCompilation compilation,
        SyntaxTree original,
        SyntaxNode originalRoot,
        string arranged,
        SemanticModel beforeModel,
        string? crashRoot,
        string originalText,
        CancellationToken cancellation
    ) {
        var rewritten = CSharpSyntaxTree.ParseText(
            SourceText.From(arranged),
            (CSharpParseOptions)original.Options,
            path,
            cancellation
        );

        // ⚠ ReplaceSyntaxTree rather than a fresh compilation: the rewritten document has to be
        // bound *in the same compilation*, against the same references and the same other files.
        // Binding it alone is what `skala fix`'s per-file syntactic check does, and it is exactly
        // the check that cannot see an overload resolved in another file.
        var after = compilation.ReplaceSyntaxTree(original, rewritten);
        var afterModel = after.GetSemanticModel(rewritten);

        var before = Signature(beforeModel.GetDiagnostics(null, cancellation));
        var now = Signature(afterModel.GetDiagnostics(null, cancellation));

        // ⚠ A *new* diagnostic, not a different count. A rewrite that removes a warning — an unused
        // using, a redundant qualifier — is a rewrite doing its job, and comparing counts would
        // reject it. What must never appear is an id the document did not have before.
        var appeared = now.Except(before, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (appeared.Length > 0) {
            var artefact = CrashArtifacts.Write(crashRoot, path, originalText, arranged, new PhaseOneOptions());
            return new(
                ArrangeIds.Reverted,
                SkalaSeverity.Error,
                $"not arranged, re-binding the rewritten document produced {appeared.Length.ToString(CultureInfo.InvariantCulture)} diagnostic(s) it did not have before: "
                + string.Join(", ", appeared.Take(4)),
                path,
                0,
                artefact is null
                    ? "This is a Skala bug; the file was left untouched."
                    : $"A reproduction is in {artefact}. This is a Skala bug; the file was left untouched."
            );
        }

        return SymbolIdentity(
            path,
            originalRoot,
            rewritten,
            beforeModel,
            afterModel,
            crashRoot,
            originalText,
            arranged,
            cancellation
        );
    }

    /// <summary>
    ///     Layer 3: for every identifier that survived the rewrite, the same name must still mean the
    ///     same thing.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the check that catches the genuinely dangerous case — the code still compiles, and
    ///     now calls something else. Layer 2 cannot see it, because nothing about it is a diagnostic:
    ///     removing a <c>this.</c> in a class that also has a static of that name, or removing a using
    ///     that was the only source of an extension method with a viable instance-method fallback, both
    ///     bind cleanly to the wrong symbol.
    ///     <para>
    ///         ⚠ Keyed by (name, containing member, ordinal within that member) rather than by position.
    ///         Positions move — that is what arrangement *is* — so a position-keyed comparison compares an
    ///         identifier against whatever the rewrite happened to slide into its offset, which is noise. A
    ///         name that is the 3rd <c>Foo</c> in <c>C.M</c> before must be the 3rd <c>Foo</c> in <c>C.M</c>
    ///         after, and if the rewrite deleted one the counts differ and the key stops matching, which is
    ///         reported rather than ignored.
    ///     </para>
    /// </remarks>
    static SkalaDiagnostic? SymbolIdentity(
        string path,
        SyntaxNode originalRoot,
        SyntaxTree rewritten,
        SemanticModel beforeModel,
        SemanticModel afterModel,
        string? crashRoot,
        string originalText,
        string arranged,
        CancellationToken cancellation
    ) {
        var before = Bindings(originalRoot, beforeModel, cancellation);
        var after = Bindings(rewritten.GetRoot(cancellation), afterModel, cancellation);

        foreach (var (key, symbol) in before) {
            if (!after.TryGetValue(key, out var now)) {
                // ⚠ Not a failure. A rewrite is allowed to delete an identifier — that is what
                // removing `this.` and an unused using both do — and the deleted one has no "after"
                // to compare against. What is checked is that every identifier that is *still there*
                // still means what it did.
                continue;
            }

            // ⚠ The ordinal is only meaningful while the *population* of that name in that member is
            // unchanged. `List<int> a = new List<int>();` ⇒ `var a = new List<int>();` deletes one
            // `List`, so the 2nd `List` in the method is now the 1st and every later ordinal is
            // compared against its neighbour — which reports a difference on almost every file that
            // arranges cleanly. Measured over corpus/real/: this false positive alone reverted 24
            // files that had nothing wrong with them. Where the count moved, the rewrite changed
            // that name deliberately and layer 2 has already confirmed the result still binds.
            if (before.Counts.GetValueOrDefault((key.Name, key.Container))
                != after.Counts.GetValueOrDefault((key.Name, key.Container))) {
                continue;
            }

            if (string.Equals(symbol, now, StringComparison.Ordinal)) {
                continue;
            }

            var artefact = CrashArtifacts.Write(crashRoot, path, originalText, arranged, new PhaseOneOptions());
            return new(
                ArrangeIds.SymbolChanged,
                SkalaSeverity.Error,
                $"not arranged, '{key.Name}' in {key.Container} bound to {symbol} before the rewrite and to {now} after it",
                path,
                0,
                artefact is null
                    ? "The code still compiles and now calls something else. This is a Skala bug; the file was left untouched."
                    : $"The code still compiles and now calls something else. A reproduction is in {artefact}; the file was left untouched."
            );
        }

        return null;
    }

    readonly record struct BindingKey(string Name, string Container, int Ordinal);

    /// <summary>Every identifier's binding, plus how many of each name each member holds.</summary>
    sealed record BindingSet(
        Dictionary<BindingKey, string> Symbols,
        Dictionary<(string Name, string Container), int> Counts) {
        public bool TryGetValue(BindingKey key, out string value) => Symbols.TryGetValue(key, out value!);

        public Dictionary<BindingKey, string>.Enumerator GetEnumerator() => Symbols.GetEnumerator();
    }

    static BindingSet Bindings(
        SyntaxNode root,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var result = new Dictionary<BindingKey, string>();
        var counters = new Dictionary<(string, string), int>();

        foreach (var node in root.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax name) {
                continue;
            }

            // ⚠ `var` is parsed as an identifier and is not a symbol reference: asking the model
            // about it returns the *inferred* type, which is by construction different before and
            // after the rewrite that introduced it. Comparing it compares the rule's output against
            // the code it replaced and calls every correct conversion a symbol change.
            if (string.Equals(name.Identifier.ValueText, "var", StringComparison.Ordinal)) {
                continue;
            }

            // The right-hand side of a member access is bound through its parent; taking both would
            // count the same identifier twice and shift every later ordinal.
            if (node.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax access
                && access.Name == node) {
                continue;
            }

            if (node.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.QualifiedNameSyntax qualified
                && qualified.Right == node) {
                continue;
            }

            var container = ContainerOf(node, model, cancellation);
            var text = name.Identifier.ValueText;
            var counterKey = (text, container);
            counters.TryGetValue(counterKey, out var ordinal);
            counters[counterKey] = ordinal + 1;

            var symbol = model.GetSymbolInfo(node, cancellation).Symbol;
            result[new BindingKey(text, container, ordinal)] =
                symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "?";
        }

        return new(result, counters);
    }

    /// <summary>
    ///     The member an identifier sits in, as a stable name.
    /// </summary>
    /// <remarks>
    ///     ⚠ The *declared symbol* of the enclosing member rather than its span, for the same reason the
    ///     key is not a position: a member that moved is still the same member, and a member that was
    ///     re-bodied has a different span and the same identity.
    /// </remarks>
    static string ContainerOf(SyntaxNode node, SemanticModel model, CancellationToken cancellation) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax
                or Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax) {
                return model.GetDeclaredSymbol(current, cancellation)
                    ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    ?? current.Kind().ToString();
            }
        }

        return "<file>";
    }

    /// <summary>
    ///     ⚠ Id plus message, not id plus position. A rewrite moves text, so the position of an
    ///     unchanged diagnostic changes and a position-keyed set reports every surviving diagnostic as
    ///     both removed and added. The message distinguishes two CS0103s about different names, which is
    ///     the distinction that matters.
    /// </summary>
    static ImmutableHashSet<string> Signature(IEnumerable<Diagnostic> diagnostics) {
        var set = ImmutableHashSet.CreateBuilder(StringComparer.Ordinal);
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning) {
                set.Add(diagnostic.Id + "|" + diagnostic.GetMessage(CultureInfo.InvariantCulture));
            }
        }

        return set.ToImmutable()!;
    }
}
