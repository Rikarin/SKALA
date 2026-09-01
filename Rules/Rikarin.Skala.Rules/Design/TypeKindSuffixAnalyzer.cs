using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6022</c> — a type name whose suffix repeats the declaration's own keyword.</summary>
/// <remarks>
///     <c>class OrderClass</c> spends a word on what the keyword two positions to its left already
///     says, and the suffix survives the edit that makes it wrong — a <c>class</c> that becomes a
///     <c>record</c> keeps its <c>Class</c> suffix until somebody notices.
///     <para>
///         ⚠ <c>Record</c> is not one of the suffixes, although the proposal names it.
///         <c>LogRecord</c>, <c>AuditRecord</c>, <c>DnsRecord</c> and <c>ActivationRecord</c> use a
///         domain noun that predates the keyword by decades, no syntactic test separates the two, and
///         the noun is far the more common. Missing <c>PersonRecord</c> is the price of not reporting
///         <c>LogRecord</c>.
///     </para>
///     <para>
///         ⚠ The compound-word exemptions are judgement, not measurement, and the list is incomplete by
///         construction. English compounds are not enumerable; the rule ships at <c>suggestion</c>
///         because that is the honest severity for a decision of this kind.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeKindSuffixAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     The PascalCase word before the suffix, where the suffix is part of a word rather than a
    ///     restatement of the keyword.
    /// </summary>
    /// <remarks>
    ///     Two different exemptions live here. In <c>MasterClass</c>, <c>CharacterClass</c>,
    ///     <c>EquivalenceClass</c>, <c>StorageClass</c>, <c>BusinessClass</c> and <c>WeightClass</c> the
    ///     word "class" means *category* and is not about C# at all. In <c>BaseClass</c>,
    ///     <c>InnerClass</c>, <c>NestedClass</c>, <c>AbstractClass</c> and <c>TestClass</c> the type is
    ///     *about* a class — a fixture, a sample, a node in a tool that reads C# — so the word is the
    ///     head noun; <c>TestClass</c> is MSTest's own attribute name and a term of art besides.
    /// </remarks>
    static readonly HashSet<string> CompoundWords = new(StringComparer.Ordinal) {
        "Base",
        "Sub",
        "Super",
        "Inner",
        "Outer",
        "Nested",
        "Abstract",
        "Sealed",
        "Partial",
        "Generic",
        "Derived",
        "Test",
        "Master",
        "Character",
        "Equivalence",
        "Storage",
        "Business",
        "Economy",
        "First",
        "Second",
        "Weight",
        "Size",
        "Device",
        "Object",
        "Working",
        "Worker",
        "Ref",
        "Readonly"
    };

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TypeNameRestatesItsKind);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        var suffix = SuffixFor(declaration);
        if (suffix is null) {
            return;
        }

        var name = declaration.Identifier.ValueText;

        // ⚠ Strictly longer, so `class Class` and `struct Struct` — a parser's or a code generator's
        // subject matter — are never reported. There the word is the whole name, not a suffix.
        if (name.Length <= suffix.Length || !name.EndsWith(suffix, StringComparison.Ordinal)) {
            return;
        }

        var prefix = name.Substring(0, name.Length - suffix.Length);

        // The suffix has to begin a new PascalCase word after a real one. An all-caps run running
        // into it is not a boundary anybody wrote deliberately.
        var last = prefix[prefix.Length - 1];
        if (!char.IsLower(last) && !char.IsDigit(last)) {
            return;
        }

        if (CompoundWords.Contains(LastWord(prefix))) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + name
                + "` ends in `"
                + suffix
                + "` and is declared `"
                + Keywords(declaration)
                + "`; the kind is already in the declaration, so `"
                + prefix
                + "` says the same thing"
            )
        );
    }

    /// <summary>
    ///     The declaration's kind as it is written — <c>class</c>, <c>record</c>, <c>record struct</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not just <c>Keyword</c>. For a record that is <c>record</c>, and a message reading "which
    ///     the `record` keyword already says" about a `Class` suffix would be saying something untrue
    ///     about the very thing the rule claims to have read.
    /// </remarks>
    static string Keywords(TypeDeclarationSyntax declaration) =>
        declaration is RecordDeclarationSyntax { ClassOrStructKeyword.RawKind: not 0 } record
            ? record.Keyword.ValueText + " " + record.ClassOrStructKeyword.ValueText
            : declaration.Keyword.ValueText;

    /// <summary>
    ///     The suffix this declaration's own keyword makes redundant, or <c>null</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The kind and the suffix must agree. A <c>class</c> named <c>PointStruct</c> is not
    ///     redundant, it is wrong, and "delete the suffix" is the wrong advice for a name that lies
    ///     about its kind — that is a different finding and this rule does not claim it.
    /// </remarks>
    static string? SuffixFor(TypeDeclarationSyntax declaration) =>
        declaration switch {
            ClassDeclarationSyntax => "Class",
            StructDeclarationSyntax => "Struct",
            RecordDeclarationSyntax record =>
                record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "Struct" : "Class",
            _ => null
        };

    /// <summary>The trailing PascalCase word of a prefix — <c>PrivateSetterTest</c> gives <c>Test</c>.</summary>
    static string LastWord(string prefix) {
        for (var i = prefix.Length - 1; i >= 0; i--) {
            if (char.IsUpper(prefix[i])) {
                return prefix.Substring(i);
            }
        }

        return prefix;
    }
}
