using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The constructs <c>csharp_new_line_before_open_brace</c> can put a brace on its own line, as the
///     C# formatter actually groups them.
/// </summary>
/// <remarks>
///     ⚠ <b>Seven groups, not twelve.</b> The key's domain names twelve code elements and
///     <c>jb cleanupcode</c> 2025.2.6 answers to seven of them; the other five are covered by a
///     neighbour and move nothing on their own. Measured 2026-08-30, one value per run, on a probe where
///     every body holds two statements and every initializer six elements so that no
///     <c>place_simple_*</c> or <c>max_*_on_line</c> rule could join a construct and hide the brace this
///     was asking about — the first attempt at this probe had one-statement bodies, and it read
///     <c>accessors</c>, <c>lambdas</c> and <c>object_collection_array_initializers</c> as inert when
///     all three are live:
///     <code>
/// types          class, struct, interface, enum, record — AND a block namespace
/// methods        method, constructor, operator bodies — AND local functions
/// properties     the accessor LIST of a property, an indexer or an event alike
/// accessors      the accessor BODY: get, set, add, remove
/// control_blocks if, while, for, foreach, do, lock, using, try/catch/finally, switch statement
/// lambdas        a lambda body — AND an anonymous method's `delegate(…) { }`
/// object_…       an initializer's braces, an anonymous type's — AND a switch EXPRESSION's
///                and a property pattern's
///
/// anonymous_methods   inert — covered by `lambdas`
/// anonymous_types     inert — covered by `object_collection_array_initializers`
/// events              inert — covered by `properties`
/// indexers            inert — covered by `properties`
/// local_functions     inert — covered by `methods`
///     </code>
///     ⚠ Three of those groupings are the ones a reading of the names would get wrong, and each was
///     checked with the neighbour that would have explained it away: <c>local_functions</c> moves
///     nothing while <c>methods</c> moves the local function; <c>events</c> and <c>indexers</c> move
///     nothing while <c>properties</c> moves all three accessor lists; <c>anonymous_methods</c> moves
///     nothing while <c>lambdas</c> moves the <c>delegate</c>.
///     <para>
///         ⚠ <c>all</c> is exactly the union of the seven, confirmed against the same probe rather than
///         assumed — a construct that no named group reaches would otherwise be invisible in both
///         directions.
///     </para>
/// </remarks>
[Flags]
public enum BraceOwners {
    None = 0,
    Types = 1,
    Methods = 2,
    Properties = 4,
    Accessors = 8,
    ControlBlocks = 16,
    Lambdas = 32,
    Initializers = 64,

    /// <summary>The key's <c>all</c>, measured to be the union of the seven live groups.</summary>
    All = Types | Methods | Properties | Accessors | ControlBlocks | Lambdas | Initializers
}

/// <summary>Reading the key, and classifying the brace it governs.</summary>
public static class BraceOwnerSet {
    /// <summary>
    ///     The key's comma-separated value as the set of constructs it puts on their own line.
    /// </summary>
    /// <remarks>
    ///     ⚠ An unrecognised member contributes nothing rather than throwing. The registry validates the
    ///     domain, and a formatter that refuses to lay out a file because one member of a flags option
    ///     was misspelled fails in the worst available way.
    /// </remarks>
    public static BraceOwners Parse(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return BraceOwners.None;
        }

        var owners = BraceOwners.None;
        foreach (var member in value.Split(',')) {
            owners |= member.Trim() switch {
                "all" => BraceOwners.All,
                "types" => BraceOwners.Types,
                "methods" => BraceOwners.Methods,
                "properties" => BraceOwners.Properties,
                "accessors" => BraceOwners.Accessors,
                "control_blocks" => BraceOwners.ControlBlocks,
                "lambdas" => BraceOwners.Lambdas,
                "object_collection_array_initializers" => BraceOwners.Initializers,

                // ⚠ `none`, and the five members the C# formatter does not answer to. Listed rather
                // than left to the default arm so that "measured inert" and "not a member of this
                // option" are two different lines of code.
                "none" => BraceOwners.None,
                "anonymous_methods" or "anonymous_types" or "events" or "indexers" or "local_functions" =>
                    BraceOwners.None,
                _ => BraceOwners.None
            };
        }

        return owners;
    }

    /// <summary>Which group an open brace belongs to.</summary>
    /// <remarks>
    ///     ⚠ Every shape <c>CSharpDocumentBuilder.OpensAJoinableBody</c> admits is classified here, and
    ///     the two must stay in step: a brace this returns <see cref="BraceOwners.None" /> for is one no
    ///     value of the key can ever move, which is a silent hole rather than a visible one.
    /// </remarks>
    public static BraceOwners Of(SyntaxToken brace) =>
        brace.Parent switch {
            BaseTypeDeclarationSyntax or NamespaceDeclarationSyntax => BraceOwners.Types,
            AccessorListSyntax => BraceOwners.Properties,

            // ⚠ A switch *expression* and a property pattern are in the initializer group. Measured,
            // and not what their syntax suggests: they are the two shapes of the nine that a reading
            // of the key's domain would have left unclassified.
            InitializerExpressionSyntax
                or AnonymousObjectCreationExpressionSyntax
                or SwitchExpressionSyntax
                or PropertyPatternClauseSyntax => BraceOwners.Initializers,

            SwitchStatementSyntax => BraceOwners.ControlBlocks,
            BlockSyntax block => OfBlock(block),
            _ => BraceOwners.None
        };

    static BraceOwners OfBlock(BlockSyntax block) =>
        block.Parent switch {
            AccessorDeclarationSyntax => BraceOwners.Accessors,
            BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax => BraceOwners.Methods,
            AnonymousFunctionExpressionSyntax => BraceOwners.Lambdas,

            // Every other statement block: `if`, `while`, `for`, `foreach`, `do`, `lock`, `using`,
            // `try` / `catch` / `finally`, `else`, and a bare nested block.
            _ => BraceOwners.ControlBlocks
        };
}
