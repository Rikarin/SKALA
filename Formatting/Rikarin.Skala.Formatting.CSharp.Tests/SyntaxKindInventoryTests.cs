using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The build-time half of the R5 mitigation (docs/plan/16).
/// </summary>
/// <remarks>
///     ⚠ <c>Testing/corpus/syntax-kinds.txt</c> is a committed inventory of every
///     <see cref="SyntaxKind" /> in the pinned Roslyn, with the layout the document builder gives it. A
///     package bump that adds a kind makes this test fail, which turns "silently mangles new syntax"
///     into "fails after a package bump" — and the run-time fallback (<see cref="NodeLayout.Unknown" />
///     ⇒ verbatim) means that even the failing build would not have corrupted a file.
///     <para>
///         Regenerating the inventory is a deliberate edit with the new kind classified, never a test that
///         rewrites it.
///     </para>
/// </remarks>
public sealed class SyntaxKindInventoryTests {
    static string InventoryPath => Path.Combine(Corpus.Root, "syntax-kinds.txt");

    static IReadOnlyDictionary<string, string> Inventory { get; } = File.ReadAllLines(InventoryPath)
        .Where(static line => line.Length > 0 && !line.StartsWith('#'))
        .Select(static line => line.Split('\t'))
        .ToDictionary(static parts => parts[0], static parts => parts[1], StringComparer.Ordinal);

    [Fact]
    public void EveryKindRoslynDeclares_IsInTheInventory() {
        var missing = Enum.GetValues<SyntaxKind>()
            .Select(static kind => kind.ToString())
            .Distinct(StringComparer.Ordinal)
            .Where(name => !Inventory.ContainsKey(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Roslyn declares {missing.Length.ToString(CultureInfo.InvariantCulture)} SyntaxKind(s) the document builder has never been told about: "
            + string.Join(", ", missing)
            + ". Classify each in NodeLayouts.Classify and add it to Testing/corpus/syntax-kinds.txt. "
            + "Until then the builder emits them verbatim, which is safe and wrong."
        );
    }

    [Fact]
    public void EveryKindInTheInventory_StillExists() {
        var known = Enum.GetValues<SyntaxKind>()
            .Select(static kind => kind.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var gone = Inventory.Keys.Where(name => !known.Contains(name)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            gone.Length == 0,
            "The inventory names kinds Roslyn no longer declares: " + string.Join(", ", gone)
        );
    }

    [Fact]
    public void EveryNodeKind_HasALayoutAndNoneIsUnknown() {
        var unknown = new StringBuilder();
        foreach (var kind in Enum.GetValues<SyntaxKind>()) {
            if (!NodeLayouts.IsNodeKind(kind)) {
                continue;
            }

            if (NodeLayouts.Classify(kind) == NodeLayout.Unknown) {
                unknown.Append(kind).Append(' ');
            }
        }

        Assert.True(unknown.Length == 0, "Unclassified node kinds: " + unknown);
    }

    [Fact]
    public void TheInventory_RecordsTheLayoutTheBuilderActuallyUses() {
        foreach (var (name, expected) in Inventory) {
            if (!Enum.TryParse<SyntaxKind>(name, out var kind) || !NodeLayouts.IsNodeKind(kind)) {
                continue;
            }

            Assert.Equal(expected, NodeLayouts.Classify(kind).ToString());
        }
    }

    [Fact]
    public void ANodeKindTheBuilderDoesNotKnow_IsEmittedVerbatim() {
        // The run-time half. There is no way to synthesise a kind Roslyn does not have, so the
        // property is asserted on the path itself: Unknown routes to the verbatim emitter.
        Assert.Equal(NodeLayout.Unknown, NodeLayouts.Classify((SyntaxKind)9999));
    }
}
