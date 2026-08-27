using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3001</c> — an <c>async void</c> method that is not an event handler.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000". An <c>async void</c> method cannot be awaited, so its
///     caller cannot know when it finished and cannot observe its exceptions. An exception thrown after
///     the first <c>await</c> is raised on whatever context resumed the method, which in a console or
///     server process is an unhandled exception that ends it.
///     <para>
///         ⚠
///         <b>
///             This rule is <see cref="RuleScope.Compilation" />-scoped, and that is the expensive
///             decision.
///         </b> The one legitimate <c>async void</c> is an event handler, and whether a method is
///         one is not visible in the file that declares it — the <c>+=</c> may be anywhere. So the rule
///         collects every name used as a method group across the whole compilation and reports only the
///         methods no such use names. That is what makes zero false positives reachable
///         (docs/plan/16 § R3), and the price is that <see cref="RuleInfo.IsCacheable" /> is false for
///         <c>SK3001</c>: a compilation with it enabled cannot use the per-file warm path
///         (docs/plan/07 § "The incremental cache"). The trade is stated rather than hidden, and a
///         repository that would rather have the cache turns the rule off.
///     </para>
///     <para>
///         ⚠ The name-based check is deliberately over-broad: any <em>occurrence</em> of the identifier in
///         a non-invoked position silences the rule, even one that refers to something else entirely. It
///         errs towards silence, which is the direction doc 00's false-positive bar asks for.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncVoidMethod);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var eventArgs = start.Compilation.GetTypeByMetadataName("System.EventArgs");
                var candidates = new ConcurrentBag<Candidate>();

                // ⚠ Every identifier that is *not* the callee of an invocation: `Handler` in
                // `x.Changed += Handler`, in `new EventHandler(Handler)`, in `Register(Handler)`.
                // One set for the whole compilation, filled during the same walk the rules already
                // make.
                var referenced = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

                start.RegisterSyntaxNodeAction(
                    context => RecordReference(context, referenced),
                    SyntaxKind.IdentifierName
                );

                start.RegisterSyntaxNodeAction(
                    context => Collect(context, candidates, eventArgs),
                    SyntaxKind.MethodDeclaration
                );

                start.RegisterCompilationEndAction(context => {
                        foreach (var candidate in candidates) {
                            if (referenced.ContainsKey(candidate.Name)) {
                                continue;
                            }

                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    Descriptor,
                                    candidate.Location,
                                    candidate.Fix,
                                    "`async void "
                                    + candidate.Name
                                    + "` cannot be awaited and its exceptions "
                                    + "cannot be caught; return `Task`"
                                )
                            );
                        }
                    }
                );
            }
        );
    }

    /// <summary>
    ///     An <c>async void</c> method that survived every local guard, pending the name check.
    /// </summary>
    /// <remarks>
    ///     ⚠ A class rather than a positional record: this assembly is <c>netstandard2.0</c>
    ///     (ADR-006) and <c>init</c> accessors need an <c>IsExternalInit</c> the target framework does
    ///     not carry. Not worth a second shim for three fields.
    /// </remarks>
    sealed class Candidate {
        public Candidate(string name, Location location, ImmutableDictionary<string, string?> fix) {
            Name = name;
            Location = location;
            Fix = fix;
        }

        public string Name { get; }

        public Location Location { get; }

        public ImmutableDictionary<string, string?> Fix { get; }
    }

    static void RecordReference(SyntaxNodeAnalysisContext context, ConcurrentDictionary<string, byte> referenced) {
        var identifier = (IdentifierNameSyntax)context.Node;

        // `Foo()` — a direct call is not a method group and says nothing about a delegate.
        if (identifier.Parent is InvocationExpressionSyntax invocation
            && ReferenceEquals(invocation.Expression, identifier)) {
            return;
        }

        // `x.Foo()` — the same, one level in.
        if (identifier.Parent is MemberAccessExpressionSyntax access
            && ReferenceEquals(access.Name, identifier)
            && access.Parent is InvocationExpressionSyntax outer
            && ReferenceEquals(outer.Expression, access)) {
            return;
        }

        referenced.TryAdd(identifier.Identifier.ValueText, 0);
    }

    static void Collect(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<Candidate> candidates,
        INamedTypeSymbol? eventArgs
    ) {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsAsyncVoid(method) || method.Body is null && method.ExpressionBody is null) {
            return;
        }

        // ⚠ Each of these is a shape where the signature is somebody else's contract rather than
        // this author's choice, so changing the return type is not available as a fix and the
        // finding would be advice nobody can take.
        //
        // ⚠ `virtual` and `new` are on the list beside `override` for the reason the fixture set
        // found: a virtual `async void` is the *top* of a dispatch chain, so its return type binds
        // every override in every assembly that derives from it. Excluding only `override` reports
        // the base declaration and calls the derived one clean, which is exactly backwards.
        foreach (var modifier in method.Modifiers) {
            if (modifier.IsKind(SyntaxKind.OverrideKeyword)
                || modifier.IsKind(SyntaxKind.VirtualKeyword)
                || modifier.IsKind(SyntaxKind.AbstractKeyword)
                || modifier.IsKind(SyntaxKind.NewKeyword)
                || modifier.IsKind(SyntaxKind.PartialKeyword)
                || modifier.IsKind(SyntaxKind.ExternKeyword)) {
                return;
            }
        }

        // An attribute is nearly always a framework binding the signature is part of — a command
        // handler, a message subscriber, a benchmark. Silence is cheap here and being wrong is not.
        if (method.AttributeLists.Count > 0) {
            return;
        }

        // The naming convention for a handler, honoured because it is what the ecosystem writes.
        if (method.Identifier.ValueText.StartsWith("On", StringComparison.Ordinal)) {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
        if (symbol is null || HasEventHandlerShape(symbol, eventArgs) || ImplementsAnInterface(symbol)) {
            return;
        }

        candidates.Add(
            new Candidate(
                method.Identifier.ValueText,
                method.Identifier.GetLocation(),
                Fix(context, method)
            )
        );
    }

    static bool IsAsyncVoid(MethodDeclarationSyntax method) {
        var async = false;
        foreach (var modifier in method.Modifiers) {
            if (modifier.IsKind(SyntaxKind.AsyncKeyword)) {
                async = true;
                break;
            }
        }

        return async
            && method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };
    }

    /// <summary><c>(object, TEventArgs)</c> — the delegate shape the BCL's events use.</summary>
    static bool HasEventHandlerShape(IMethodSymbol method, INamedTypeSymbol? eventArgs) {
        if (eventArgs is null || method.Parameters.Length != 2) {
            return false;
        }

        if (method.Parameters[0].Type.SpecialType != SpecialType.System_Object) {
            return false;
        }

        for (var type = method.Parameters[1].Type; type is not null; type = type.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(type, eventArgs)) {
                return true;
            }
        }

        return false;
    }

    static bool ImplementsAnInterface(IMethodSymbol method) {
        if (!method.ExplicitInterfaceImplementations.IsEmpty) {
            return true;
        }

        var containing = method.ContainingType;
        if (containing is null) {
            return false;
        }

        foreach (var @interface in containing.AllInterfaces) {
            foreach (var member in @interface.GetMembers(method.Name)) {
                if (SymbolEqualityComparer.Default.Equals(
                        containing.FindImplementationForInterfaceMember(member),
                        method
                    )) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Replace <c>void</c> with the task type, spelled the way it binds at this position.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Task</c> when the simple name resolves here, the fully qualified name when it does not.
    ///     A fix that emits <c>Task</c> into a file with no <c>using System.Threading.Tasks;</c> is
    ///     CS0246, and <c>FixCommand</c>'s post-fix check is syntactic — it re-parses and compares
    ///     syntax diagnostics — so a missing using would pass verification and break the build. The
    ///     lookup is the only thing that makes the fix safe to emit at all, which is also why the rule
    ///     declares <c>fixIsSafe: false</c>: changing a return type changes callers.
    /// </remarks>
    static ImmutableDictionary<string, string?> Fix(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method) {
        var position = method.ReturnType.SpanStart;
        var name = "System.Threading.Tasks.Task";
        foreach (var symbol in context.SemanticModel.LookupNamespacesAndTypes(position, name: "Task")) {
            if (symbol is INamedTypeSymbol { Arity: 0 } type
                && string.Equals(type.ToDisplayString(), "System.Threading.Tasks.Task", StringComparison.Ordinal)) {
                name = "Task";
                break;
            }
        }

        return FixEdits.Pack((method.ReturnType.Span, name));
    }
}
