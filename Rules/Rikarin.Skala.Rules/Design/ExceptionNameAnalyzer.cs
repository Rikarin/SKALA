using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6021</c> — a type named <c>…Exception</c> that does not derive from one.</summary>
/// <remarks>
///     The <c>Exception</c> suffix is the one .NET naming convention a language construct depends on: a
///     reader who sees it writes <c>catch (ThatException)</c>, and against a type that is not an
///     exception that line does not compile.
///     <para>
///         ⚠ The suffix, never the substring. <c>ExceptionHandler</c>, <c>ExceptionFilter</c> and
///         <c>ExceptionPolicy</c> are correctly named and are not exceptions — and each ends in the word
///         that says what it really is, which is why matching the *end* of the name is the whole
///         containment answer rather than a list of special cases.
///     </para>
///     <para>
///         ⚠ Semantic, and an unresolved base type is silence. A walk up the base chain answers "does
///         not derive from <c>Exception</c>" and "the compilation could not tell" identically, and only
///         one of those is a finding.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExceptionNameAnalyzer : DiagnosticAnalyzer {
    const string Suffix = "Exception";

    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.ExceptionNameWithoutExceptionBase);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var exception = start.Compilation.GetTypeByMetadataName("System.Exception");
                if (exception is null) {
                    // No `System.Exception` in the reference set is not a compilation this rule can
                    // say anything about.
                    return;
                }

                start.RegisterSymbolAction(symbol => Analyze(symbol, exception), SymbolKind.NamedType);
            }
        );
    }

    static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol exception) {
        var type = (INamedTypeSymbol)context.Symbol;

        // ⚠ Only the kinds that *could* derive from Exception. An interface named `IFooException`
        // cannot, so the finding would name no available repair; enums and delegates likewise.
        // Structs stay in: one cannot be an exception either, but there the name is what changes.
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)) {
            return;
        }

        // ⚠ `Length <=` and not `<`: a type named exactly `Exception` is either `System.Exception`
        // itself or a deliberate shadow, and "derive from `Exception`" says nothing to either.
        if (type.Name.Length <= Suffix.Length || !type.Name.EndsWith(Suffix, StringComparison.Ordinal)) {
            return;
        }

        if (DerivesFromOrIsUnknown(type, exception)) {
            return;
        }

        foreach (var location in type.Locations) {
            if (!location.IsInSource) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    location,
                    "`"
                    + type.Name
                    + "` is named like an exception and does not derive from `System.Exception`, so `catch ("
                    + type.Name
                    + ")` will not compile; derive from it or rename the type"
                )
            );

            return;
        }
    }

    /// <summary>Whether the type reaches <paramref name="exception" />, or the answer is unknowable.</summary>
    /// <remarks>
    ///     ⚠ The two are one method on purpose. An error type in the chain means the compilation could
    ///     not resolve a base, and reporting then would turn one missing reference into a naming finding
    ///     on every exception type in the assembly.
    /// </remarks>
    static bool DerivesFromOrIsUnknown(INamedTypeSymbol type, INamedTypeSymbol exception) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.TypeKind == TypeKind.Error) {
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(current, exception)) {
                return true;
            }
        }

        return false;
    }
}
