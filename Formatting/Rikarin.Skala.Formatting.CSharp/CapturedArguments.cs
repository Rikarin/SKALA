using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The expressions whose source text a <c>[CallerArgumentExpression]</c> parameter may capture at
///     run time, found from one file's syntax alone (#432).
/// </summary>
/// <remarks>
///     ⚠ <b>The token stream is not the whole story.</b> The compiler hands the captured argument's
///     source text — whitespace and line breaks included — to the program as a string, so
///     <c>Check(a   &lt;   b)</c> prints <c>[a   &lt;   b]</c> and, formatted, <c>[a &lt; b]</c>. Neither of
///     Skala's two promises sees that: the tokens are identical (<c>SK9099</c> is silent) and the file
///     parses (<c>SK9010</c> is silent). So the formatter leaves every span this returns byte-identical,
///     the way it already leaves an interpolated string. <c>jb cleanupcode</c> reformats them
///     (SK-DIV-0187).
///     <para>
///         ⚠ <b>Syntactic in every load mode, and deliberately an over-approximation.</b> The formatter
///         runs per file with no compilation, and <c>format --check</c> has to give the same answer in CI
///         as on a laptop that passed <c>--load</c>; a span preserved under one load mode and formatted
///         under another is a check that fails in one place and passes in the other. So the answer is a
///         function of the file alone: a call whose callee <em>could</em> capture is preserved, and the
///         cost of a wrong guess is one argument left as the author wrote it. The measured comparison
///         with the semantic answer (<c>CallerArgumentSafety</c>) is in SK-DIV-0187.
///     </para>
///     <para>
///         Two sources of "could capture":
///         <list type="number">
///             <item>
///                 <see cref="Known" />: every capturing member of the .NET 10 reference pack, NUnit 4,
///                 xunit 2/3 and Shouldly, enumerated from the assemblies rather than remembered — see
///                 its remarks for what was found and what was not.
///             </item>
///             <item>
///                 Every method, local function, constructor, indexer, attribute constructor and delegate type declared
///                 <em>in this file</em> with a <c>[CallerArgumentExpression]</c> parameter, matched at a
///                 call site by name. ⚠ A capturing method declared in <b>another</b> file is not seen —
///                 the one false negative of the syntactic answer, and the measured one.
///             </item>
///         </list>
///     </para>
/// </remarks>
public static class CapturedArguments {
    /// <summary>
    ///     A capturing member of a library: the receiver type's simple name, the member, which positional
    ///     arguments are captured, and from which positional count on the caller has supplied the
    ///     capturing parameter itself.
    /// </summary>
    readonly record struct Api(string Type, string Method, int[] Captured, int SuppliedAt, string[] CapturingNames);

    static readonly string[] Message = ["message"];
    static readonly string[] ParamName = ["paramName"];
    static readonly string[] NUnitNames = ["actualExpression", "constraintExpression"];
    static readonly int[] First = [0];
    static readonly int[] FirstTwo = [0, 1];

    /// <summary>
    ///     ⚠ Measured, not remembered: every public parameter carrying
    ///     <c>CallerArgumentExpressionAttribute</c> in <c>Microsoft.NETCore.App.Ref</c> 10.0.12,
    ///     <c>NUnit</c> 4.2.2, <c>xunit.assert</c> 2.9.3, <c>xunit.v3.assert</c> 4.0.0 and
    ///     <c>Shouldly</c> 4.3.0, enumerated through Roslyn's symbol model on 2026-10-07.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three things the issue and the brief assumed and the assemblies refuted:
    ///     <list type="bullet">
    ///         <item><c>ObjectDisposedException.ThrowIf</c> captures nothing.</item>
    ///         <item>
    ///             Neither xunit captures anything — not v2, not v3 4.0.0 — and neither does Shouldly. A
    ///             rule of "every <c>Assert.*</c>" would preserve 3 402 <c>Assert.Equal</c> calls in the
    ///             corpus alone for no reason.
    ///         </item>
    ///         <item>
    ///             <c>Trace.Assert</c> captures, exactly as <c>Debug.Assert</c> does.
    ///         </item>
    ///     </list>
    ///     NUnit captures <em>both</em> of the first two arguments of <c>Assert.That</c> and its relatives
    ///     — the actual value and the constraint — into two separate parameters. ⚠ MSTest was not in the
    ///     local package cache and is <b>not measured</b>; it is absent rather than guessed at.
    /// </remarks>
    static readonly Api[] Known = [
        new("Debug", "Assert", First, 2, Message),
        new("Trace", "Assert", First, 2, Message),
        new("ArgumentNullException", "ThrowIfNull", First, 2, ParamName),
        new("ArgumentException", "ThrowIfNullOrEmpty", First, 2, ParamName),
        new("ArgumentException", "ThrowIfNullOrWhiteSpace", First, 2, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfZero", First, 2, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfNegative", First, 2, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfNegativeOrZero", First, 2, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfEqual", First, 3, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfNotEqual", First, 3, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfGreaterThan", First, 3, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfGreaterThanOrEqual", First, 3, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfLessThan", First, 3, ParamName),
        new("ArgumentOutOfRangeException", "ThrowIfLessThanOrEqual", First, 3, ParamName),
        new("Assert", "That", FirstTwo, int.MaxValue, NUnitNames),
        new("Assert", "ThatAsync", FirstTwo, int.MaxValue, NUnitNames),
        new("Assert", "ByVal", FirstTwo, int.MaxValue, NUnitNames),
        new("Assume", "That", FirstTwo, int.MaxValue, NUnitNames),
        new("Warn", "If", FirstTwo, int.MaxValue, NUnitNames),
        new("Warn", "Unless", FirstTwo, int.MaxValue, NUnitNames)
    ];

    enum CallKind {
        Invocation,
        Creation,
        Element,

        /// <summary>
        ///     A delegate type's <c>Invoke</c>: called through a variable, so matched through the names
        ///     declared with that type in this file.
        /// </summary>
        Delegate
    }

    /// <summary>A capturing parameter declared in this file.</summary>
    sealed record Declared(
        CallKind Kind,
        string Name,
        int CapturedIndex,
        string CapturedName,
        int CapturingIndex,
        string CapturingName,
        bool IsParams,
        bool IsThis,
        bool IsExtensionReceiver);

    /// <summary>
    ///     The spans of every expression in <paramref name="root" /> whose text may be captured, sorted
    ///     and distinct. Empty for almost every file.
    /// </summary>
    public static ImmutableArray<TextSpan> Find(SyntaxNode root) {
        List<Declared>? declared = null;
        List<SyntaxNode>? calls = null;
        HashSet<string>? staticImports = null;

        foreach (var node in root.DescendantNodes()) {
            switch (node) {
                case ParameterSyntax { AttributeLists.Count: > 0 } parameter:
                    if (Declare(parameter) is { } declaration) {
                        (declared ??= []).Add(declaration);
                    }

                    break;

                case UsingDirectiveSyntax { StaticKeyword.RawKind: not 0, Name: { } name }:
                    (staticImports ??= new HashSet<string>(StringComparer.Ordinal)).Add(Rightmost(name));
                    break;

                case InvocationExpressionSyntax
                    or BaseObjectCreationExpressionSyntax
                    or ConstructorInitializerSyntax
                    or PrimaryConstructorBaseTypeSyntax
                    or ElementAccessExpressionSyntax
                    or ElementBindingExpressionSyntax
                    or AttributeSyntax:
                    (calls ??= []).Add(node);
                    break;
            }
        }

        if (calls is null) {
            return [];
        }

        // ⚠ A delegate is invoked through a variable, never by its type's name, and measured in #422:
        // `Checker check = …; check(x => x + 12)` captures. So the names declared with the delegate's
        // type in this file stand in for the type's name.
        var delegates = declared?.Any(static declaration => declaration.Kind == CallKind.Delegate) == true
            ? DelegateVariables(root, declared)
            : null;

        var spans = new HashSet<TextSpan>();
        foreach (var call in calls) {
            if (call is InvocationExpressionSyntax invocation) {
                MatchKnown(invocation, staticImports, spans);
            }

            if (declared is not null) {
                foreach (var declaration in declared) {
                    if (declaration.Kind == CallKind.Delegate) {
                        MatchDelegate(call, declaration, delegates!, spans);
                    } else {
                        MatchDeclared(call, declaration, spans);
                    }
                }
            }
        }

        if (spans.Count == 0) {
            return [];
        }

        var sorted = spans.ToList();
        sorted.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));
        return [.. sorted];
    }

    /// <summary>Whether <paramref name="span" /> lies inside one of <paramref name="captured" />.</summary>
    public static bool Within(ImmutableArray<TextSpan> captured, TextSpan span) {
        foreach (var region in captured) {
            if (region.Contains(span)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether inserting text at <paramref name="position" /> changes a captured string: strictly
    ///     inside one, since text at either edge lands outside the expression.
    /// </summary>
    public static bool Interior(ImmutableArray<TextSpan> captured, int position) {
        foreach (var region in captured) {
            if (position > region.Start && position < region.End) {
                return true;
            }
        }

        return false;
    }

    static void MatchKnown(
        InvocationExpressionSyntax invocation,
        HashSet<string>? staticImports,
        HashSet<TextSpan> spans
    ) {
        string method;
        string? type;
        switch (invocation.Expression) {
            case MemberAccessExpressionSyntax { Name: { } name } access:
                method = name.Identifier.ValueText;
                type = RightmostOrNull(access.Expression);
                break;
            case SimpleNameSyntax name when staticImports is not null:
                method = name.Identifier.ValueText;
                type = null;
                break;
            default:
                return;
        }

        foreach (var api in Known) {
            if (api.Method != method) {
                continue;
            }

            // ⚠ A bare `Assert(x)` counts only under a `using static` of the type, which is the one way
            // the call can bind to it without naming it.
            if (type is null ? !staticImports!.Contains(api.Type) : type != api.Type) {
                continue;
            }

            var arguments = invocation.ArgumentList.Arguments;
            if (Supplies(arguments, api.SuppliedAt, api.CapturingNames)) {
                continue;
            }

            foreach (var index in api.Captured) {
                AddArgument(arguments, index, null, spans);
            }
        }
    }

    static void MatchDelegate(
        SyntaxNode call,
        Declared declaration,
        Dictionary<string, HashSet<string>> variables,
        HashSet<TextSpan> spans
    ) {
        if (call is not InvocationExpressionSyntax invocation
            || !variables.TryGetValue(declaration.Name, out var names)) {
            return;
        }

        // `check(…)`, `this.check(…)`, `check.Invoke(…)` and `check?.Invoke(…)`.
        var callee = invocation.Expression switch {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Invoke" } access => RightmostOrNull(
                access.Expression
            ),
            MemberBindingExpressionSyntax { Name.Identifier.ValueText: "Invoke" } =>
                invocation.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()?.Expression is { } receiver
                    ? RightmostOrNull(receiver)
                    : null,
            var expression => CalleeName(expression)
        };
        if (callee is null || !names.Contains(callee)) {
            return;
        }

        AddDeclared(invocation, invocation.ArgumentList.Arguments, declaration, spans);
    }

    /// <summary>Delegate type name to the names of the locals, parameters, fields and properties of that type.</summary>
    static Dictionary<string, HashSet<string>> DelegateVariables(SyntaxNode root, List<Declared> declared) {
        var types = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var declaration in declared) {
            if (declaration.Kind == CallKind.Delegate) {
                types.TryAdd(declaration.Name, new HashSet<string>(StringComparer.Ordinal));
            }
        }

        foreach (var node in root.DescendantNodes()) {
            switch (node) {
                case VariableDeclarationSyntax variable when types.TryGetValue(Rightmost(variable.Type), out var names):
                    foreach (var declarator in variable.Variables) {
                        names.Add(declarator.Identifier.ValueText);
                    }

                    break;
                case ParameterSyntax { Type: { } type } parameter when types.TryGetValue(
                    Rightmost(type),
                    out var names
                ):
                    names.Add(parameter.Identifier.ValueText);
                    break;
                case PropertyDeclarationSyntax property when types.TryGetValue(Rightmost(property.Type), out var names):
                    names.Add(property.Identifier.ValueText);
                    break;
            }
        }

        return types;
    }

    static void MatchDeclared(SyntaxNode call, Declared declaration, HashSet<TextSpan> spans) {
        switch (call) {
            case InvocationExpressionSyntax invocation when declaration.Kind == CallKind.Invocation:
                if (CalleeName(invocation.Expression) != declaration.Name) {
                    return;
                }

                if (Supplies(
                        invocation.ArgumentList.Arguments,
                        declaration.CapturingIndex + 1,
                        [declaration.CapturingName]
                    )) {
                    return;
                }

                // ⚠ Measured in #422: a `params` capture is the whole call, not the argument.
                if (declaration.IsParams) {
                    spans.Add(invocation.Span);
                    return;
                }

                var receiver = Receiver(invocation.Expression);
                if (declaration.IsExtensionReceiver) {
                    if (receiver is not null) {
                        spans.Add(receiver.Span);
                    }

                    return;
                }

                // ⚠ An extension method's call can be written two ways and syntax cannot tell them
                // apart: reduced, where `this` is the receiver and every other argument shifts left by
                // one, and static, where nothing shifts. Both readings are preserved.
                if (declaration.IsThis && receiver is not null) {
                    if (declaration.CapturedIndex == 0) {
                        spans.Add(receiver.Span);
                    } else {
                        AddArgument(
                            invocation.ArgumentList.Arguments,
                            declaration.CapturedIndex - 1,
                            declaration.CapturedName,
                            spans
                        );
                    }
                }

                AddArgument(
                    invocation.ArgumentList.Arguments,
                    declaration.CapturedIndex,
                    declaration.CapturedName,
                    spans
                );
                return;

            case BaseObjectCreationExpressionSyntax { ArgumentList: { } arguments } creation
                when declaration.Kind == CallKind.Creation:
                // ⚠ `new(…)` names no type, so every capturing constructor in the file is a candidate.
                if (creation is ObjectCreationExpressionSyntax { Type: var type }
                    && Rightmost(type) != declaration.Name) {
                    return;
                }

                AddDeclared(creation, arguments.Arguments, declaration, spans);
                return;

            // `this(…)` and `base(…)`: the target type is not written, so any capturing constructor.
            case ConstructorInitializerSyntax initializer when declaration.Kind == CallKind.Creation:
                AddDeclared(initializer, initializer.ArgumentList.Arguments, declaration, spans);
                return;

            case PrimaryConstructorBaseTypeSyntax baseType when declaration.Kind == CallKind.Creation:
                if (Rightmost(baseType.Type) == declaration.Name) {
                    AddDeclared(baseType, baseType.ArgumentList.Arguments, declaration, spans);
                }

                return;

            // An indexer has no name at the call site, so every element access is a candidate.
            case ElementAccessExpressionSyntax access when declaration.Kind == CallKind.Element:
                AddDeclared(access, access.ArgumentList.Arguments, declaration, spans);
                return;

            case ElementBindingExpressionSyntax binding when declaration.Kind == CallKind.Element:
                AddDeclared(binding, binding.ArgumentList.Arguments, declaration, spans);
                return;

            case AttributeSyntax { ArgumentList: { } attributeArguments } attribute
                when declaration.Kind == CallKind.Creation:
                var name = Rightmost(attribute.Name);
                if (name != declaration.Name && name + "Attribute" != declaration.Name) {
                    return;
                }

                var positional = 0;
                foreach (var argument in attributeArguments.Arguments) {
                    if (argument.NameEquals is not null) {
                        continue;
                    }

                    if (argument.NameColon is { } colon
                            ? colon.Name.Identifier.ValueText == declaration.CapturedName
                            : positional == declaration.CapturedIndex) {
                        spans.Add(Unparenthesised(argument.Expression)!.Span);
                    }

                    positional++;
                }

                return;
        }
    }

    static void AddDeclared(
        SyntaxNode call,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        Declared declaration,
        HashSet<TextSpan> spans
    ) {
        if (Supplies(arguments, declaration.CapturingIndex + 1, [declaration.CapturingName])) {
            return;
        }

        if (declaration.IsParams) {
            spans.Add(call.Span);
            return;
        }

        AddArgument(arguments, declaration.CapturedIndex, declaration.CapturedName, spans);
    }

    /// <summary>
    ///     Whether the caller wrote the capturing parameter itself, in which case the compiler captures
    ///     nothing: by name, or by a positional count that reaches it.
    /// </summary>
    static bool Supplies(SeparatedSyntaxList<ArgumentSyntax> arguments, int suppliedAt, string[] names) {
        var positional = 0;
        foreach (var argument in arguments) {
            if (argument.NameColon is { } colon) {
                if (Array.IndexOf(names, colon.Name.Identifier.ValueText) >= 0) {
                    return true;
                }

                continue;
            }

            positional++;
        }

        return positional >= suppliedAt;
    }

    static void AddArgument(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int index,
        string? name,
        HashSet<TextSpan> spans
    ) {
        if (name is not null) {
            foreach (var argument in arguments) {
                if (argument.NameColon?.Name.Identifier.ValueText == name) {
                    spans.Add(Unparenthesised(argument.Expression)!.Span);
                    return;
                }
            }
        }

        if (index >= 0 && index < arguments.Count && arguments[index].NameColon is null) {
            spans.Add(Unparenthesised(arguments[index].Expression)!.Span);
        }
    }

    /// <summary>The capturing parameter this one is, or null.</summary>
    static Declared? Declare(ParameterSyntax parameter) {
        if (CapturedNameOf(parameter) is not { } captured
            || captured == parameter.Identifier.ValueText
            || parameter.Parent is not BaseParameterListSyntax list) {
            return null;
        }

        var (kind, name) = list.Parent switch {
            MethodDeclarationSyntax method => (CallKind.Invocation, method.Identifier.ValueText),
            LocalFunctionStatementSyntax local => (CallKind.Invocation, local.Identifier.ValueText),
            ConstructorDeclarationSyntax constructor => (CallKind.Creation, constructor.Identifier.ValueText),
            TypeDeclarationSyntax type => (CallKind.Creation, type.Identifier.ValueText),
            IndexerDeclarationSyntax => (CallKind.Element, "this"),
            DelegateDeclarationSyntax @delegate => (CallKind.Delegate, @delegate.Identifier.ValueText),
            _ => ((CallKind?)null, string.Empty)
        };
        if (kind is not { } callKind) {
            return null;
        }

        var parameters = list.Parameters;
        var capturing = parameters.IndexOf(parameter);
        for (var i = 0; i < parameters.Count; i++) {
            if (parameters[i].Identifier.ValueText != captured) {
                continue;
            }

            return new(
                callKind,
                name,
                i,
                captured,
                capturing,
                parameter.Identifier.ValueText,
                parameters[i].Modifiers.Any(SyntaxKind.ParamsKeyword),
                i == 0 && parameters[i].Modifiers.Any(SyntaxKind.ThisKeyword),
                false
            );
        }

        // ⚠ A C# 14 extension block's receiver is the block's parameter, not the member's.
        if (list.Parent is MethodDeclarationSyntax {
                Parent: ExtensionBlockDeclarationSyntax { ParameterList: { } block }
            }
            && block.Parameters.Any(candidate => candidate.Identifier.ValueText == captured)) {
            return new(
                callKind,
                name,
                -1,
                captured,
                capturing,
                parameter.Identifier.ValueText,
                false,
                false,
                true
            );
        }

        return null;
    }

    /// <summary>The name a <c>[CallerArgumentExpression(…)]</c> on this parameter names, or null.</summary>
    static string? CapturedNameOf(ParameterSyntax parameter) {
        foreach (var list in parameter.AttributeLists) {
            foreach (var attribute in list.Attributes) {
                var name = Rightmost(attribute.Name);
                if (name is not ("CallerArgumentExpression" or "CallerArgumentExpressionAttribute")
                    || attribute.ArgumentList is not { Arguments: [{ Expression: var expression }] }) {
                    continue;
                }

                return expression switch {
                    LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                        literal.Token.ValueText,
                    InvocationExpressionSyntax {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" },
                        ArgumentList.Arguments: [{ Expression: var named }]
                    } => RightmostOrNull(named),
                    _ => null
                };
            }
        }

        return null;
    }

    static string? CalleeName(ExpressionSyntax callee) =>
        callee switch {
            SimpleNameSyntax name => name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => null
        };

    /// <summary>The receiver expression of a member call, or null for a bare one.</summary>
    static ExpressionSyntax? Receiver(ExpressionSyntax callee) => Unparenthesised(ReceiverOf(callee));

    /// <summary>The expression inside every enclosing pair of parentheses.</summary>
    /// <remarks>
    ///     ⚠ Measured, SDK 10.0, and it refuted the first version of this type: the compiler captures an
    ///     argument or a receiver <b>without</b> its parentheses, at any depth.
    ///     <c>Check(( a   &lt;   b ))</c> captures <c>a   &lt;   b</c>, <c>Check(((a&lt;b)))</c> captures
    ///     <c>a&lt;b</c>, and <c>(a   +   b).Text()</c> captures <c>a   +   b</c> — so the gaps between a
    ///     parenthesis and what it encloses are not captured, and are formatted. A cast's or a tuple's
    ///     parentheses are not this node and are kept: <c>(object)(a  +  b)</c> and <c>(a,  b)</c> are
    ///     captured whole. ⚠ <c>CallerArgumentSafety.Source</c> (#422) answers with the written
    ///     argument, parentheses included — wider than the capture, and harmless in the direction it errs.
    /// </remarks>
    static ExpressionSyntax? Unparenthesised(ExpressionSyntax? expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesised) {
            expression = parenthesised.Expression;
        }

        return expression;
    }

    static ExpressionSyntax? ReceiverOf(ExpressionSyntax callee) =>
        callee switch {
            MemberAccessExpressionSyntax access => access.Expression,
            MemberBindingExpressionSyntax { Parent: InvocationExpressionSyntax invocation } =>
                invocation.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()?.Expression,
            _ => null
        };

    static string Rightmost(SyntaxNode name) => RightmostOrNull(name) ?? string.Empty;

    static string? RightmostOrNull(SyntaxNode node) =>
        node switch {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            NullableTypeSyntax nullable => RightmostOrNull(nullable.ElementType),
            _ => null
        };
}
