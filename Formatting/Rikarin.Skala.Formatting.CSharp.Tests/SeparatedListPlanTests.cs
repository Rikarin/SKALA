using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;
using System.Reflection;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Every syntax node kind that holds a <see cref="SeparatedSyntaxList{TNode}" /> is either planned by
///     <see cref="BreakPlan" /> or exempted here with a reason (issue #371, SK-DIV-0114).
/// </summary>
/// <remarks>
///     ⚠ The set is reflected over <c>Microsoft.CodeAnalysis.CSharp.Syntax</c>, never hand-listed:
///     <c>BracketedParameterListSyntax</c> went unplanned for as long as the list of "lists the
///     planner visits" was a sentence in a code comment (SK-DIV-0108), and eleven more were found the
///     day somebody enumerated the kinds instead. A Roslyn bump that adds a list kind fails this test
///     until the kind is planned or exempted, which is the point. "Planned" is asked of the plan
///     itself: a sample holding the kind is planned and the node is expected to carry a group — an
///     outer one, an inner one, or a constraint run — so removing a routing from
///     <see cref="BreakPlan" /> reddens the row that names the kind.
/// </remarks>
public sealed class SeparatedListPlanTests {
    /// <summary>
    ///     The kinds the planner deliberately does not visit, each with the measurement behind it. A
    ///     kind that is not planned and not here fails <see cref="EveryKindHoldingASeparatedList_IsPlannedOrExempted" />.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, string> Exempt = new Dictionary<Type, string> {
        [typeof(TupleTypeSyntax)] =
            "the oracle never breaks a tuple type at a comma — one past the margin breaks between an element's type "
            + "and its name, or outside the type — and a kept break inside one is keep_user_linebreaks' (SK-DIV-0114)",
        [typeof(CrefParameterListSyntax)] =
            "inside a documentation comment, which the walk never enters; the xmldoc formatter keeps a break there "
            + "and so does the oracle profile that formats doc comments (SK-DIV-0006, SK-DIV-0114)",
        [typeof(CrefBracketedParameterListSyntax)] =
            "inside a documentation comment, as CrefParameterListSyntax (SK-DIV-0114)",
        [typeof(PragmaWarningDirectiveTriviaSyntax)] =
            "a directive: the trivia model owns it and it never reaches the walker (NodeLayout.DirectiveNode)",
        [typeof(OrderByClauseSyntax)] =
            "`orderby a,\\n b` is unmeasured; the query's clauses are PlanQuery's and its orderings are left as "
            + "written, recorded open in SK-DIV-0114",
        [typeof(AllowsConstraintClauseSyntax)] =
            "`allows ref struct` is the one constraint the clause admits, so the list never has a comma"
    };

    /// <summary>
    ///     One source per planned kind holding that kind. ⚠ Not the inventory — the inventory is the
    ///     reflection below — but the witness that the kind is planned: a kind with neither a sample nor
    ///     an exemption fails.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, string> Samples = new Dictionary<Type, string> {
        [typeof(ArgumentListSyntax)] = "class C { void M() { F(1, 2); } }",
        [typeof(BracketedArgumentListSyntax)] = "class C { int M(int[,] g) => g[0, 1]; }",
        [typeof(AttributeArgumentListSyntax)] = "[System.Obsolete(\"a\", true)] class C { }",
        [typeof(AttributeListSyntax)] = "[System.Obsolete, System.Serializable] class C { }",
        [typeof(ParameterListSyntax)] = "class C { void M(int a, int b) { } }",
        [typeof(BracketedParameterListSyntax)] = "class C { int this[int a, int b] => a; }",
        [typeof(TypeParameterListSyntax)] = "class C<T, U> { }",
        [typeof(TypeArgumentListSyntax)] = "class C { System.Collections.Generic.Dictionary<int, int> F; }",
        [typeof(TupleExpressionSyntax)] = "class C { object M() => (1, 2); }",
        [typeof(PositionalPatternClauseSyntax)] = "class C { bool M(object o) => o is (1, 2); }",
        [typeof(PropertyPatternClauseSyntax)] = "class C { bool M(object o) => o is { A: 1 }; }",
        [typeof(ListPatternSyntax)] = "class C { bool M(int[] o) => o is [1, 2]; }",
        [typeof(CollectionExpressionSyntax)] = "class C { int[] F = [1, 2]; }",
        [typeof(InitializerExpressionSyntax)] = "class C { int[] F = new[] { 1, 2 }; }",
        [typeof(AnonymousObjectCreationExpressionSyntax)] = "class C { object F = new { A = 1, B = 2 }; }",
        [typeof(BaseListSyntax)] = "class C : System.IDisposable, System.ICloneable { }",
        [typeof(VariableDeclarationSyntax)] = "class C { void M() { int a = 1, b = 2; } }",
        [typeof(EnumDeclarationSyntax)] = "enum E { A, B }",
        [typeof(SwitchExpressionSyntax)] = "class C { int M(int x) => x switch { 1 => 1, _ => 0 }; }",
        [typeof(ForStatementSyntax)] = "class C { void M() { for (int i = 0, j = 0; i < 1; i++, j++) { } } }",
        [typeof(ParenthesizedVariableDesignationSyntax)] = "class C { void M() { var (a, b) = (1, 2); } }",
        [typeof(ArrayRankSpecifierSyntax)] = "class C { object F = new int[1, 2]; }",
        [typeof(FunctionPointerParameterListSyntax)] = "unsafe class C { delegate*<int, void> F; }",
        [typeof(FunctionPointerUnmanagedCallingConventionListSyntax)] =
            "unsafe class C { delegate* unmanaged[Cdecl, Stdcall]<int, void> F; }",
        [typeof(TypeParameterConstraintClauseSyntax)] = "class C<T> where T : class, new() { }"
    };

    /// <summary>
    ///     Every concrete node type in <c>Microsoft.CodeAnalysis.CSharp.Syntax</c> with a public property
    ///     of type <see cref="SeparatedSyntaxList{TNode}" />.
    /// </summary>
    static Type[] KindsHoldingASeparatedList { get; } = typeof(CSharpSyntaxNode).Assembly
        .GetTypes()
        .Where(static type => type.Namespace == "Microsoft.CodeAnalysis.CSharp.Syntax")
        .Where(static type => type is { IsClass: true, IsAbstract: false } && type.IsSubclassOf(typeof(CSharpSyntaxNode)))
        .Where(
            static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(
                    static property => property.PropertyType.IsGenericType
                        && property.PropertyType.GetGenericTypeDefinition() == typeof(SeparatedSyntaxList<>)
                )
        )
        .OrderBy(static type => type.Name, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void TheReflectedSet_IsNotTrivial() {
        // ⚠ Anti-vacuity: a reflection that matched nothing would pass the assertion below for free.
        Assert.True(
            KindsHoldingASeparatedList.Length >= 25,
            $"only {KindsHoldingASeparatedList.Length.ToString(CultureInfo.InvariantCulture)} kinds hold a separated list: "
            + string.Join(", ", KindsHoldingASeparatedList.Select(static type => type.Name))
        );

        Assert.Contains(typeof(ArgumentListSyntax), KindsHoldingASeparatedList);
        Assert.Contains(typeof(BracketedParameterListSyntax), KindsHoldingASeparatedList);
    }

    [Fact]
    public void EveryKindHoldingASeparatedList_IsPlannedOrExempted() {
        var unaccounted = KindsHoldingASeparatedList
            .Where(static type => !Exempt.ContainsKey(type) && !Samples.ContainsKey(type))
            .Select(static type => type.Name)
            .ToArray();

        Assert.True(
            unaccounted.Length == 0,
            "Kinds holding a SeparatedSyntaxList that BreakPlan neither plans nor exempts: "
            + string.Join(", ", unaccounted)
            + ". Measure each against the oracle (Testing ask) and either route it in BreakPlan.Plan with a "
            + "sample here, or exempt it with the measurement as the reason."
        );

        var both = Exempt.Keys.Intersect(Samples.Keys).Select(static type => type.Name).ToArray();
        Assert.True(both.Length == 0, "Both planned and exempted: " + string.Join(", ", both));

        var stale = Exempt.Keys.Concat(Samples.Keys)
            .Where(type => !KindsHoldingASeparatedList.Contains(type))
            .Select(static type => type.Name)
            .ToArray();

        Assert.True(stale.Length == 0, "Named here but holding no separated list: " + string.Join(", ", stale));
    }

    [Fact]
    public void EverySampledKind_IsPlanned() {
        var planned = 0;
        var unplanned = new List<string>();
        foreach (var (type, source) in Samples.OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal)) {
            var tree = CSharpSyntaxTree.ParseText(source, CSharpFormatter.ParseOptions, cancellationToken: TestContext.Current.CancellationToken);
            var root = tree.GetRoot(TestContext.Current.CancellationToken);
            var node = root.DescendantNodes().FirstOrDefault(candidate => candidate.GetType() == type);
            Assert.True(node is not null, $"the sample for {type.Name} holds no {type.Name}: {source}");

            var plan = BreakPlan.Build(root, source, Format.Options);
            if (plan.GroupsOf(node!).Count > 0 || plan.TryInnerGroup(node!, out _) || plan.TryConstraintRun(node!, out _)) {
                planned++;
            } else {
                unplanned.Add(type.Name);
            }
        }

        Assert.True(
            unplanned.Count == 0,
            "BreakPlan.Plan puts no group on these kinds, so a break inside them is left exactly as written: "
            + string.Join(", ", unplanned)
            + ". Route each to its twin's plan or exempt it with a measurement (SK-DIV-0114)."
        );

        // ⚠ Anti-vacuity: the planned set is non-empty, and it is most of the reflected set.
        Assert.True(planned >= 20, $"only {planned.ToString(CultureInfo.InvariantCulture)} kinds are planned");
    }
}
