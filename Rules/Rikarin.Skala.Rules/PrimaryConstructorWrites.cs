using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>
///     The one question <c>SK2194</c> and <c>SK4022</c> both ask: which primary-constructor parameters
///     does the type write once it has been constructed?
/// </summary>
/// <remarks>
///     ⚠ The definition is the compiler's, read off <c>readonly struct</c>: a reference is a write
///     exactly where the modifier would turn it into <c>CS9114</c> (assigned), <c>CS9115</c> (returned by
///     writable reference), <c>CS9116</c> (used as a <c>ref</c>/<c>out</c> value), <c>CS9117</c> (a
///     member modified) or <c>CS9119</c> (a member used as a <c>ref</c>/<c>out</c> value) — and the
///     compiler's own two exceptions, a variable initializer and an <c>init</c> accessor, are construction
///     rather than mutation. Every form was compiled under <c>readonly struct</c> for #408.
///     <para>
///         ⚠ It lives here because the two rules disagreed. <c>SK2194</c> matched four syntax shapes
///         over a bare identifier, so a deconstruction <c>(start, end) = (end, start)</c> and a member
///         write <c>origin.X = x</c> were both invisible to it, while <c>SK4022</c> — fixed for #402 —
///         already declined on both. A second copy of this would be a second chance for them to drift.
///     </para>
///     <para>
///         ⚠ <b>One deliberate departure from the compiler.</b> A member write through a capture whose
///         type is an <em>unconstrained</em> type parameter compiles under <c>readonly struct</c> — the
///         compiler writes to a defensive copy — but when the argument is a struct it writes the capture
///         itself today, and adding <c>readonly</c> would silently redirect the write to a copy. It
///         counts: the climb through a member access stops only at a type known to be a reference type.
///     </para>
/// </remarks>
static class PrimaryConstructorWrites {
    /// <summary>
    ///     The primary-constructor parameters of <paramref name="declaration" /> that code running after
    ///     construction writes, in declaration order.
    /// </summary>
    public static IReadOnlyList<IParameterSymbol> WrittenParameters(
        TypeDeclarationSyntax declaration,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        if (declaration.ParameterList is not { Parameters.Count: > 0 } parameters) {
            return [];
        }

        var names = new HashSet<string>(
            parameters.Parameters.Select(static parameter => parameter.Identifier.ValueText),
            StringComparer.Ordinal
        );
        var written = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);

        // ⚠ Only the type's own syntax. A nested type cannot see these parameters, and a same-named
        // parameter of its own is a different symbol that the span test below would reject anyway.
        foreach (var identifier in declaration
                     .DescendantNodes(node => node == declaration || node is not TypeDeclarationSyntax)
                     .OfType<IdentifierNameSyntax>()) {
            if (names.Contains(identifier.Identifier.ValueText)
                && model.GetSymbolInfo(identifier, cancellation).Symbol is IParameterSymbol parameter
                && parameter.DeclaringSyntaxReferences.Any(reference => parameters.Span.Contains(reference.Span))
                && !written.Contains(parameter)
                && IsWritten(identifier, model, cancellation)
                && RunsAfterConstruction(identifier)) {
                written.Add(parameter);
            }
        }

        return parameters.Parameters
            .Select(parameter => model.GetDeclaredSymbol(parameter, cancellation))
            .Where(symbol => symbol is not null && written.Contains(symbol))
            .Select(static symbol => symbol!)
            .ToArray();
    }

    /// <summary>
    ///     Whether the storage <paramref name="reference" /> names is written, through any parenthesis,
    ///     tuple element, or member or element access of a value-typed receiver between it and the write.
    /// </summary>
    /// <remarks>
    ///     ⚠ The receiver test is what keeps a captured array's <c>items[0] = x</c> and a class-typed
    ///     capture's <c>holder.Count = 1</c> out: both write an object the capture points at, never the
    ///     capture. A struct capture's field, property setter, indexer setter and tuple element are the
    ///     capture's own storage, and <c>CS9117</c> says so for each.
    /// </remarks>
    static bool IsWritten(ExpressionSyntax reference, SemanticModel model, CancellationToken cancellation) {
        var node = reference;
        while (true) {
            switch (node.Parent) {
                case ParenthesizedExpressionSyntax parenthesized:
                    node = parenthesized;
                    continue;
                case ArgumentSyntax { Parent: TupleExpressionSyntax tuple }:
                    node = tuple;
                    continue;
                case MemberAccessExpressionSyntax access when access.Expression == node
                    && ValueReceiver(node, model, cancellation):
                    node = access;
                    continue;
                case ElementAccessExpressionSyntax element when element.Expression == node
                    && ValueReceiver(node, model, cancellation):
                    node = element;
                    continue;
            }

            break;
        }

        // ⚠ A `readonly` setter does not write its receiver, so `readonly struct` accepts it.
        if (node != reference
            && node is MemberAccessExpressionSyntax or ElementAccessExpressionSyntax
            && model.GetSymbolInfo(node, cancellation).Symbol is IPropertySymbol { SetMethod.IsReadOnly: true }) {
            return false;
        }

        return IsWriteTarget(node, model, cancellation);
    }

    static bool ValueReceiver(ExpressionSyntax receiver, SemanticModel model, CancellationToken cancellation) =>
        model.GetTypeInfo(receiver, cancellation).Type is not { IsReferenceType: true };

    /// <summary>
    ///     Whether <paramref name="node" /> is itself the target of a write: assigned (simply, compoundly
    ///     or by deconstruction), incremented, decremented, passed as <c>ref</c> or <c>out</c>, or bound to
    ///     a writable reference.
    /// </summary>
    /// <remarks>
    ///     <c>in</c> arguments, <c>ref readonly</c> locals and <c>ref readonly</c> reassignments read; each
    ///     compiles under <c>readonly struct</c>. ⚠ A <c>ref</c> argument to a <c>ref readonly</c>
    ///     parameter is still <c>CS9116</c>, so the argument's keyword decides, not the parameter's.
    /// </remarks>
    public static bool IsWriteTarget(ExpressionSyntax node, SemanticModel model, CancellationToken cancellation) =>
        node.Parent switch {
            AssignmentExpressionSyntax assignment => assignment.Left == node,
            RefExpressionSyntax reference => BindsWritableReference(reference, model, cancellation),
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            _ => false
        };

    /// <summary>
    ///     <c>ref int r = ref a</c> is <c>CS9116</c>; <c>ref readonly int r = ref a</c> compiles, and so
    ///     does the same through a conditional's arms or a <c>ref readonly</c> local reassigned.
    /// </summary>
    /// <remarks>
    ///     A <c>ref</c> return is always counted: a writable one is <c>CS9115</c>, and a
    ///     <c>ref readonly</c> one of a parameter is <c>CS8166</c> in every type, so there is nothing to
    ///     tell apart.
    /// </remarks>
    static bool BindsWritableReference(
        RefExpressionSyntax reference,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        SyntaxNode node = reference;
        while (node.Parent is ConditionalExpressionSyntax conditional
               && conditional.Condition != node
               && conditional.Parent is RefExpressionSyntax outer) {
            node = outer;
        }

        var target = node.Parent switch {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                model.GetDeclaredSymbol(declarator, cancellation),
            AssignmentExpressionSyntax assignment when assignment.Right == node =>
                model.GetSymbolInfo(assignment.Left, cancellation).Symbol,
            _ => null
        };

        return target switch {
            ILocalSymbol local => local.RefKind != RefKind.RefReadOnly,
            IParameterSymbol parameter => parameter.RefKind is not (RefKind.RefReadOnly or RefKind.In),
            IFieldSymbol field => field.RefKind != RefKind.RefReadOnly,
            _ => true
        };
    }

    /// <summary>
    ///     Whether a write at <paramref name="node" /> can happen once the instance exists.
    /// </summary>
    /// <remarks>
    ///     ⚠ A variable initializer and an <c>init</c> accessor are the compiler's own two exceptions —
    ///     both are construction, and <c>readonly struct</c> accepts a write in either. ⚠ But a lambda,
    ///     local function or query <em>inside</em> an initializer runs whenever it is called. Measured for
    ///     #408 on <c>class K(int a) { readonly Action f = () =&gt; a++; public int G =&gt; a; }</c>: calling
    ///     <c>f</c> after construction leaves <c>G</c> at 2, because once a member captures the parameter
    ///     every reference to it, initializers included, is the hidden field. Before #408 an
    ///     expression-bodied lambda there was missed and a block-bodied one was not.
    /// </remarks>
    static bool RunsAfterConstruction(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case QueryExpressionSyntax:
                    return true;
                case AccessorDeclarationSyntax accessor:
                    return !accessor.IsKind(SyntaxKind.InitAccessorDeclaration);
                case EqualsValueClauseSyntax when current.Parent is VariableDeclaratorSyntax {
                    Parent.Parent: FieldDeclarationSyntax or EventFieldDeclarationSyntax
                }:
                case EqualsValueClauseSyntax when current.Parent is PropertyDeclarationSyntax:
                case ConstructorInitializerSyntax:
                case BaseListSyntax:
                case AttributeListSyntax:
                case ParameterListSyntax:
                    return false;
                case MemberDeclarationSyntax:
                    return true;
            }
        }

        return false;
    }
}
