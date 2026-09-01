using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6008</c> — an extension method whose receiver is <c>object</c>.</summary>
/// <remarks>
///     An extension on <c>object</c> appears in completion for every value in the program. The rule is
///     semantic because <c>object</c>, <c>System.Object</c>, an alias and a user type named
///     <c>Object</c> are not interchangeable questions. There is no mechanical fix: choosing the
///     narrowest useful receiver type is the API design decision the finding asks for.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionMethodOnObjectAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ExtensionMethodOnObject);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!method.Modifiers.Any(static token => token.IsKind(SyntaxKind.StaticKeyword))
            || method.ParameterList.Parameters.FirstOrDefault() is not { } receiver
            || !receiver.Modifiers.Any(static token => token.IsKind(SyntaxKind.ThisKeyword))) {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(receiver.Type!, context.CancellationToken).Type;
        if (type?.SpecialType != SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                receiver.Type!.GetLocation(),
                "`"
                + method.Identifier.ValueText
                + "` extends `object` and therefore appears on every value; narrow the receiver type"
            )
        );
    }
}
