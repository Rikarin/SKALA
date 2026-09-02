using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary>
///     The attribute vocabulary of the three test frameworks, resolved once per compilation.
/// </summary>
/// <remarks>
///     ⚠ <b>Which framework a rule understands is part of the rule, not an implementation detail.</b> The
///     three spell everything differently — MSTest requires a class attribute and xUnit has none, NUnit
///     made its own optional in NUnit 3 — so a rule that silently understands one of them reports a false
///     clean on the other two. Every rule in this namespace states in its <c>falsePositives</c> which
///     frameworks it covers, and this type is where the names it covers them by are written down once.
///     <para>
///         ⚠ Attributes are matched by <em>symbol</em>, walking the base chain, never by written name.
///         <c>DataTestMethodAttribute</c> derives from <c>TestMethodAttribute</c> and a repository's own
///         attribute may derive from either, so the base walk is what makes those covered; and somebody
///         else's type happening to be called <c>TestAttribute</c> is what makes name matching wrong.
///     </para>
///     <para>
///         ⚠ Resolution is by metadata name against the compilation, which finds a framework referenced as
///         an assembly and a framework declared in source alike. The second is what a fixture is: NUnit and
///         MSTest are not on the fixture harness's reference set, so a rule that additionally demanded the
///         symbol come from metadata would be a rule whose NUnit and MSTest fixtures prove nothing.
///     </para>
/// </remarks>
sealed class TestFrameworks {
    const string MsTest = "Microsoft.VisualStudio.TestTools.UnitTesting.";
    const string NUnit = "NUnit.Framework.";

    TestFrameworks(
        ImmutableArray<INamedTypeSymbol> testMethods,
        ImmutableArray<INamedTypeSymbol> lifecycle,
        INamedTypeSymbol? msTestClass,
        INamedTypeSymbol? nUnitFixture,
        INamedTypeSymbol? msTestMethod
    ) {
        TestMethodAttributes = testMethods;
        LifecycleAttributes = lifecycle;
        MsTestClassAttribute = msTestClass;
        NUnitFixtureAttribute = nUnitFixture;
        MsTestMethodAttribute = msTestMethod;
    }

    /// <summary>Every attribute that makes a method a test case, in any of the three frameworks.</summary>
    public ImmutableArray<INamedTypeSymbol> TestMethodAttributes { get; }

    /// <summary>
    ///     Setup and teardown hooks, which are the reason a fixture may legitimately hold no test.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> LifecycleAttributes { get; }

    /// <summary><c>[TestClass]</c>, the attribute MSTest will not discover a class without.</summary>
    public INamedTypeSymbol? MsTestClassAttribute { get; }

    /// <summary><c>[TestFixture]</c>, which NUnit 3 made optional and which still declares intent.</summary>
    public INamedTypeSymbol? NUnitFixtureAttribute { get; }

    /// <summary><c>[TestMethod]</c>, the root of the MSTest method attributes.</summary>
    public INamedTypeSymbol? MsTestMethodAttribute { get; }

    public static TestFrameworks Resolve(Compilation compilation) {
        var testMethods = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var lifecycle = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // ⚠ Only the roots of each hierarchy are listed. `DataTestMethodAttribute`,
        // `TestCaseSourceAttribute` and a repository's own derived attribute are all reached by the
        // base walk in `Carries`, and listing them here as well would say the walk is not trusted.
        var msTestMethod = compilation.GetTypeByMetadataName(MsTest + "TestMethodAttribute");
        Add(testMethods, msTestMethod);
        Add(testMethods, compilation.GetTypeByMetadataName("Xunit.FactAttribute"));
        Add(testMethods, compilation.GetTypeByMetadataName("Xunit.TheoryAttribute"));
        Add(testMethods, compilation.GetTypeByMetadataName(NUnit + "TestAttribute"));
        Add(testMethods, compilation.GetTypeByMetadataName(NUnit + "TestCaseAttribute"));
        Add(testMethods, compilation.GetTypeByMetadataName(NUnit + "TestCaseSourceAttribute"));
        Add(testMethods, compilation.GetTypeByMetadataName(NUnit + "TheoryAttribute"));

        foreach (var name in new[] {
                     MsTest + "AssemblyInitializeAttribute", MsTest + "AssemblyCleanupAttribute",
                     MsTest + "ClassInitializeAttribute", MsTest + "ClassCleanupAttribute",
                     MsTest + "TestInitializeAttribute", MsTest + "TestCleanupAttribute", NUnit + "SetUpAttribute",
                     NUnit + "TearDownAttribute", NUnit + "OneTimeSetUpAttribute", NUnit + "OneTimeTearDownAttribute"
                 }) {
            Add(lifecycle, compilation.GetTypeByMetadataName(name));
        }

        return new(
            testMethods.ToImmutable(),
            lifecycle.ToImmutable(),
            compilation.GetTypeByMetadataName(MsTest + "TestClassAttribute"),
            compilation.GetTypeByMetadataName(NUnit + "TestFixtureAttribute"),
            msTestMethod
        );
    }

    static void Add(ImmutableArray<INamedTypeSymbol>.Builder builder, INamedTypeSymbol? type) {
        if (type is not null) {
            builder.Add(type);
        }
    }

    /// <summary>Whether a symbol carries an attribute deriving from any of <paramref name="roots" />.</summary>
    public static bool Carries(ISymbol symbol, IReadOnlyList<INamedTypeSymbol> roots) {
        foreach (var attribute in symbol.GetAttributes()) {
            if (DerivesFromAny(attribute.AttributeClass, roots)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a symbol carries an attribute deriving from one root.</summary>
    public static bool Carries(ISymbol symbol, INamedTypeSymbol? root) =>
        root is not null && Carries(symbol, new[] { root });

    /// <summary>
    ///     Whether a type holds a test case, which is how xUnit itself decides a class is a test class.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>xUnit gives a test class no attribute to read</b> (#303), so a rule excluding
    ///     <em>all</em> test code by attribute cannot see an xUnit fixture at all — and its helpers,
    ///     which carry nothing of their own, are reported. MSTest has <c>[TestClass]</c> and NUnit has
    ///     <c>[TestFixture]</c>; xUnit's discoverer looks for a method carrying <c>[Fact]</c> or
    ///     <c>[Theory]</c>, and that is what this reproduces. It is decidable from attributes alone: no
    ///     naming convention, no reference sniffing, and it works identically for the other two
    ///     frameworks.
    ///     <para>
    ///         ⚠ <b>It is not free, and the cost is stated rather than discovered later.</b> A class
    ///         holding one <c>[Fact]</c> and several production helpers has all of them excluded. That is
    ///         the price of treating "lives beside a test" as "is test code", and the shape it buys — a
    ///         settle loop polling a wall-clock deadline in a fixture — is 22 of the 38 findings
    ///         <c>SK2160</c> makes on the reference tree.
    ///     </para>
    ///     <para>
    ///         ⚠ It answers only for the type it is given. A helper in a <em>separate</em> class that
    ///         holds no test case is still not test code by this test, however plainly it lives in a test
    ///         project. Recognising that needs the compilation's references, which is a different and
    ///         coarser question, and this does not pretend to answer it.
    ///     </para>
    /// </remarks>
    public static bool HoldsATestCase(INamedTypeSymbol type, TestFrameworks frameworks) {
        foreach (var member in type.GetMembers()) {
            if (member is IMethodSymbol
                && (Carries(member, frameworks.TestMethodAttributes)
                    || Carries(member, frameworks.LifecycleAttributes))) {
                return true;
            }
        }

        return false;
    }

    static bool DerivesFromAny(INamedTypeSymbol? attribute, IReadOnlyList<INamedTypeSymbol> roots) {
        for (var current = attribute; current is not null; current = current.BaseType) {
            foreach (var root in roots) {
                if (SymbolEqualityComparer.Default.Equals(current, root)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ The one declaration of a type that may carry a finding, so a <c>partial</c> class produces
    ///     one.
    /// </summary>
    /// <remarks>
    ///     Two findings on one type would each carry the same insertion, and applying both writes an
    ///     attribute twice that <c>AllowMultiple</c> forbids. The anchor is the first declaring reference
    ///     in file-then-position order rather than "the part the analyzer happened to visit first",
    ///     because analyzers run concurrently over trees and the second answer is not a function of the
    ///     source.
    /// </remarks>
    public static bool IsAnchorDeclaration(INamedTypeSymbol type, SyntaxNode declaration) {
        SyntaxReference? anchor = null;
        foreach (var reference in type.DeclaringSyntaxReferences) {
            if (anchor is null || Compare(reference, anchor) < 0) {
                anchor = reference;
            }
        }

        return anchor is not null
            && anchor.SyntaxTree == declaration.SyntaxTree
            && anchor.Span == declaration.Span;
    }

    static int Compare(SyntaxReference left, SyntaxReference right) {
        var path = string.CompareOrdinal(left.SyntaxTree.FilePath, right.SyntaxTree.FilePath);
        return path != 0 ? path : left.Span.Start.CompareTo(right.Span.Start);
    }

    /// <summary>
    ///     The qualifier an attribute was written with, so a fix inserting a sibling attribute spells it
    ///     the same way.
    /// </summary>
    /// <remarks>
    ///     ⚠ A fix that always emitted the short name would not compile in a file that writes
    ///     <c>[Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]</c> with no <c>using</c>, and one
    ///     that always emitted the long name would be correct and unreadable everywhere else. The file
    ///     already answers the question, so it is read rather than guessed.
    /// </remarks>
    public static string Qualify(SyntaxNode attributeName, string simpleName) {
        var text = attributeName.ToString();
        var dot = text.LastIndexOf('.');
        return dot < 0 ? simpleName : text.Substring(0, dot + 1) + simpleName;
    }
}
