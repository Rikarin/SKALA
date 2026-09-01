using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2100</c> — <c>[ThreadStatic]</c> on a field where it cannot do what it was added for.
/// </summary>
/// <remarks>
///     Two shapes, one concept: the attribute is on the declaration and the declaration defeats it.
///     <list type="bullet">
///         <item>
///             On an <b>instance</b> field the attribute does nothing whatsoever. Thread-static storage is
///             per-thread storage of a <em>static</em> slot; an instance field already has one slot per
///             object and the runtime ignores the attribute entirely.
///         </item>
///         <item>
///             ⚠ On a <b>static field with an initializer</b> it does something worse than nothing. The
///             initializer runs once, in the static constructor, on whichever thread touches the type
///             first. That thread sees the value; every other thread sees <c>default</c>. Nothing fails,
///             nothing warns, and the field reads correctly in the debugger on the thread that happens to
///             be attached — which is why this is the shape people actually hit.
///         </item>
///     </list>
///     <para>
///         ⚠ The two are ordered, not combined. An instance field with an initializer is reported once,
///         as an instance field: the initializer is beside the point when the attribute is inert to
///         begin with, and two findings on one declaration would be the same defect counted twice.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IneffectiveThreadStaticAnalyzer : DiagnosticAnalyzer {
    const string ThreadStaticAttribute = "System.ThreadStaticAttribute";

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IneffectiveThreadStatic);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var field = (FieldDeclarationSyntax)context.Node;
        var marker = Marker(field, context.SemanticModel, context.CancellationToken);
        if (marker is null) {
            return;
        }

        // ⚠ A `const` is implicitly static and is *required* to carry an initializer, so neither half
        // of this rule can be true of it — and the repair the second half offers, deleting the
        // initializer, produces source that does not compile.
        if (field.Modifiers.Any(SyntaxKind.ConstKeyword)) {
            return;
        }

        if (!field.Modifiers.Any(SyntaxKind.StaticKeyword)) {
            // ⚠ The fix deletes the attribute rather than inserting `static`, and the harness is what
            // settled that. Adding `static` turns an instance field with an initializer into a
            // *static* field with an initializer, which is the other half of this very rule — so the
            // fix fired the rule again and `skala fix` would have looped. Deleting the annotation
            // makes the declaration say what it already does, in one edit, from any starting shape.
            // The other repair — making the field static and moving the initializer into an
            // accessor — changes where the value lives and which threads share it, which is why this
            // fix is not marked safe.
            var removal = AttributeContract.Removal(marker);
            if (removal is null) {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    field.GetLocation(),
                    FixEdits.Pack((removal.Value, string.Empty)),
                    "`[ThreadStatic]` on an instance field does nothing; the field is not `static`"
                )
            );

            return;
        }

        foreach (var declarator in field.Declaration.Variables) {
            if (declarator.Initializer is null
                || IsDefaultValue(
                    context.SemanticModel.GetConstantValue(
                        declarator.Initializer.Value,
                        context.CancellationToken
                    )
                )) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    declarator.Initializer.GetLocation(),
                    FixEdits.Pack(
                        (
                            TextSpan.FromBounds(declarator.Identifier.Span.End, declarator.Initializer.Span.End),
                            string.Empty
                        )
                    ),
                    "the initializer of `[ThreadStatic] "
                    + declarator.Identifier.ValueText
                    + "` runs once, on the first thread to touch the type; every other thread sees `default`"
                )
            );
        }
    }

    static AttributeSyntax? Marker(
        FieldDeclarationSyntax field,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        foreach (var list in field.AttributeLists) {
            foreach (var attribute in list.Attributes) {
                var type = AttributeContract.Resolve(model, attribute, cancellation);
                if (type is not null
                    && string.Equals(AttributeContract.NameOf(type), ThreadStaticAttribute, StringComparison.Ordinal)) {
                    return attribute;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     ⚠ <c>= 0</c>, <c>= false</c>, <c>= null</c> and <c>= default</c> are excluded, and the
    ///     exclusion is not tidiness.
    /// </summary>
    /// <remarks>
    ///     Those initializers assign exactly what every thread other than the first one already sees,
    ///     so there is no divergence between threads to report — which is the whole of what this half
    ///     of the rule is about. Reporting them would make the rule fire on a declaration that is
    ///     merely verbose, and a rule that fires on code with no defect in it is how a category gets
    ///     switched off.
    /// </remarks>
    static bool IsDefaultValue(Optional<object?> constant) =>
        constant.HasValue
        && constant.Value switch {
            null => true,
            bool value => !value,
            char value => value == '\0',
            sbyte value => value == 0,
            byte value => value == 0,
            short value => value == 0,
            ushort value => value == 0,
            int value => value == 0,
            uint value => value == 0,
            long value => value == 0,
            ulong value => value == 0,
            float value => value == 0,
            double value => value == 0,
            decimal value => value == 0,
            _ => false
        };
}
