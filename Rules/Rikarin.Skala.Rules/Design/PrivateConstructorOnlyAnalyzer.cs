using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6033</c> — a class nothing can construct and nothing can derive from.
/// </summary>
/// <remarks>
///     A private constructor closes a type to everybody except the code that can see private members,
///     and the pattern is normal: a singleton, a factory, a static holder written before
///     <c>static class</c> existed. What makes it a finding is the same private constructor with none
///     of those beside it — nothing creating the type, nothing deriving from it, and no <c>static</c>
///     to say the type was never meant to be instantiated. The type then exists and is unreachable, and
///     nothing in the compiler says so.
///     <para>
///         ⚠ <b>The reachability test is a proof rather than a heuristic, and that is why it reads the
///         whole file.</b> A private constructor is accessible only inside the declaring type and the
///         types nested in it, so for a type that is not <c>partial</c> every legal <c>new</c> of it and
///         every legal derivation from it is in this one syntax tree.
///     </para>
///     <para>
///         ⚠ <b>Nesting opens access inward, not outward, and the first draft of this rule had it
///         backwards.</b> A nested type reaches its container's private constructor — a nested builder
///         calling <c>new Pipeline(…)</c>, a nested case deriving from its container — and that is what
///         makes scanning the file rather than the candidate's own body necessary. The reverse does not
///         hold: a container calling a nested type's private constructor is <b>CS0122</b>, measured by
///         compiling it rather than reasoned about. <c>private</c> limits access to the type that
///         declares the member and to what is nested inside it, and in no other direction.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateConstructorOnlyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OnlyPrivateConstructors);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ One action per file, not per class. The question is "does anything in this tree reach the
        // type", so a per-declaration action would walk the whole file once per candidate and turn a
        // file of constants into quadratic work for an answer it computed the first time.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CompilationUnit);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var unit = (CompilationUnitSyntax)context.Node;

        var candidates = new List<ClassDeclarationSyntax>();
        foreach (var node in unit.DescendantNodes()) {
            if (node is ClassDeclarationSyntax declaration && IsClosedToEverybody(declaration)) {
                candidates.Add(declaration);
            }
        }

        if (candidates.Count == 0) {
            return;
        }

        var reached = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var node in unit.DescendantNodes()) {
            switch (node) {
                case ObjectCreationExpressionSyntax:
                case ImplicitObjectCreationExpressionSyntax:
                    // The created type, not the resolved constructor: an inaccessible constructor
                    // gives no symbol, and "somebody tried to create this here" is the fact wanted.
                    Add(reached, context.SemanticModel.GetTypeInfo(node, context.CancellationToken).Type);
                    break;

                case SimpleBaseTypeSyntax baseType:
                    Add(
                        reached,
                        context.SemanticModel.GetSymbolInfo(baseType.Type, context.CancellationToken).Symbol as ITypeSymbol
                    );
                    break;

                case TypeOfExpressionSyntax typeOf:
                    // ⚠ `typeof(Foo)` beside a private constructor is what reflection looks like from
                    // here — `Activator.CreateInstance(typeof(Foo), nonPublic: true)` and every
                    // container registration by type. The rule cannot follow it and does not guess.
                    Add(
                        reached,
                        context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type
                    );
                    break;
            }
        }

        foreach (var candidate in candidates) {
            if (context.SemanticModel.GetDeclaredSymbol(candidate, context.CancellationToken) is not { } type
                || reached.Contains(type.OriginalDefinition)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    candidate.Identifier.GetLocation(),
                    "`"
                    + candidate.Identifier.ValueText
                    + "` declares only private constructors, and nothing in this file creates it or "
                    + "derives from it, so no caller can obtain an instance; make it `static`, give it "
                    + "a factory, or widen a constructor"
                )
            );
        }
    }

    static void Add(HashSet<INamedTypeSymbol> reached, ITypeSymbol? type) {
        if (type is INamedTypeSymbol named && named.TypeKind != TypeKind.Error) {
            reached.Add(named.OriginalDefinition);
        }
    }

    /// <summary>
    ///     Whether the declaration hands its constructors to nobody outside itself.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>abstract</c> is excluded and it is the exemption most worth stating. An abstract class
    ///     with a private constructor is a hierarchy closed to its own nested types — the way C# spells
    ///     a discriminated union — and it is a deliberate and increasingly common design rather than an
    ///     unreachable type. <c>record</c> and <c>struct</c> are excluded too: a positional record has a
    ///     public primary constructor, and a struct always has an implicit parameterless one, so a
    ///     private constructor never makes either of them unreachable.
    /// </remarks>
    static bool IsClosedToEverybody(ClassDeclarationSyntax declaration) {
        foreach (var modifier in declaration.Modifiers) {
            switch ((SyntaxKind)modifier.RawKind) {
                case SyntaxKind.StaticKeyword:
                case SyntaxKind.AbstractKeyword:
                // Another part may declare a public constructor, or create the type. The whole
                // reachability proof below rests on the file holding every access to a private
                // member, and a partial type is exactly where that stops being true.
                case SyntaxKind.PartialKeyword:
                    return false;
            }
        }

        // A framework reads an attributed type — a serializer, a container, a generator — and any of
        // them can construct it through a constructor no caller in this tree names.
        if (declaration.AttributeLists.Count > 0) {
            return false;
        }

        var declared = 0;
        foreach (var member in declaration.Members) {
            if (member is not ConstructorDeclarationSyntax constructor) {
                continue;
            }

            var isStatic = false;
            var escapes = false;
            foreach (var modifier in constructor.Modifiers) {
                switch ((SyntaxKind)modifier.RawKind) {
                    case SyntaxKind.StaticKeyword:
                        isStatic = true;
                        break;

                    case SyntaxKind.PublicKeyword:
                    case SyntaxKind.ProtectedKeyword:
                    case SyntaxKind.InternalKeyword:
                        escapes = true;
                        break;
                }
            }

            // A static constructor has no accessibility and creates nothing.
            if (isStatic) {
                continue;
            }

            if (escapes) {
                return false;
            }

            // `[JsonConstructor]` and its neighbours name a private constructor for a serializer to
            // call, which is a caller this rule cannot see and must not contradict.
            if (constructor.AttributeLists.Count > 0) {
                return false;
            }

            declared++;
        }

        // ⚠ At least one *declared* constructor. A class with none has an implicit public one and is
        // not closed to anything; matching on "no public constructor" instead would report every
        // ordinary class in the language.
        return declared > 0;
    }
}
