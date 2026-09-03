using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Rikarin.Skala.Options.Generator;

internal sealed record OptionEnumValue(string EditorConfigName, string MemberName, string Summary);

internal sealed record OptionEnum(
    string Name,
    IReadOnlyList<OptionEnumValue> Values,
    IReadOnlyList<KeyValuePair<string, string>> ValueAliases);

internal enum OptionValueKind {
    Bool,
    Int,
    String,
    Enum,
    Flags
}

internal sealed record OptionEntry(
    string Key,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Export,
    string Language,
    OptionValueKind Kind,
    string? EnumName,
    string? Default,
    string DefaultSource,
    string Tier,
    string Construct,
    string Summary,
    string Since,
    string? Oracle,
    string? Docs,
    int? TemplateLine,
    bool SeveritySuffix,
    string? Inert,
    IReadOnlyList<string> Expands,
    int? Min,
    int? Max,
    string? BoundsBecause,
    string? TabMeans,
    string? FreeFormBecause,
    string? UnsweptBecause) {
    /// <summary>The <c>OptionId</c> member name and the group path from docs/plan/02 § "Naming".</summary>
    public string MemberName => Naming.Pascal(Key);

    public IReadOnlyList<string> GroupPath => Naming.GroupPath(Key);
}

internal sealed record OptionRegistry(IReadOnlyList<OptionEnum> Enums, IReadOnlyList<OptionEntry> Options);

internal static class Naming {
    static readonly string[] CSharpGroup = ["Skala"];
    static readonly string[] XmlDocGroup = ["Skala", "XmlDoc"];

    /// <summary>
    ///     Every key prefix Skala knows, most specific first.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The single authority.</b> This list used to exist four times — here,
    ///     <c>OptionResolver.SpecificityPrefixes</c>, <c>ConfigCommands.Family</c> and
    ///     <c>SweepPlan.Strip</c> — with no two of them agreeing on the whole set and nothing
    ///     comparing them. Each failed *silently* into a wrong-but-plausible answer rather than into
    ///     an error: a prefix missing from <c>Family</c> puts an option in a family named after its
    ///     vendor prefix, one missing from <c>Strip</c> makes <c>--family=space</c> quietly skip it,
    ///     and one missing here scatters the generated API. The generator emits this list as
    ///     <c>OptionKeyPrefixes.Ordered</c> so the three runtime call sites read it instead of
    ///     restating it, and <c>OptionRegistryTests.EveryPrefixConsumer_ReadsTheGeneratedList</c>
    ///     fails if a fifth copy appears.
    ///     <para>
    ///         Order is significance order and is load-bearing: <c>skala_xmldoc_</c> must be tested
    ///         before <c>skala_</c> or every xmldoc key strips to the wrong stem.
    ///     </para>
    /// </remarks>
    public static readonly string[] Prefixes = ["skala_xmldoc_", "skala_", "csharp_", "dotnet_"];

    public static string Pascal(string editorConfigName) {
        var builder = new StringBuilder(editorConfigName.Length);
        var upper = true;
        foreach (var c in editorConfigName) {
            if (c is '_' or '.' or '-' or ' ') {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        var result = builder.ToString();
        return result.Length == 0 || char.IsDigit(result[0]) ? "_" + result : result;
    }

    /// <summary>
    ///     The key prefix is retained as a group, so <c>skala_wrap_arguments_style</c> reads as
    ///     <c>Options.Skala.WrapArgumentsStyle</c> (docs/plan/02 § "Naming").
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>skala_</c> is one group where <c>resharper_csharp_</c> and <c>resharper_</c> were two.
    ///     That merge is safe only because the rename is collision-free over the whole registry —
    ///     <c>skala_x</c> and <c>skala_xmldoc_x</c> are the one pair that could have collided, and
    ///     <c>xmldoc</c> keeps its segment for exactly that reason.
    /// </remarks>
    public static IReadOnlyList<string> GroupPath(string key) =>
        key switch {
            _ when key.StartsWith("skala_xmldoc_", StringComparison.Ordinal) => XmlDocGroup,
            _ when key.StartsWith("skala_", StringComparison.Ordinal) => CSharpGroup,
            _ when key.StartsWith("csharp_", StringComparison.Ordinal) => ["CSharp"],
            _ when key.StartsWith("dotnet_", StringComparison.Ordinal) => ["DotNet"],
            _ => ["Standard"]
        };

    public static string LeafName(string key) {
        foreach (var prefix in Prefixes) {
            if (key.StartsWith(prefix, StringComparison.Ordinal)) {
                return Pascal(key.Substring(prefix.Length));
            }
        }

        return Pascal(key);
    }
}

internal static class OptionRegistryReader {
    public static OptionRegistry Read(string json) {
        var root = Json.Parse(json);

        var enums = new List<OptionEnum>();
        foreach (var member in root["enums"].Members.OrderBy(static m => m.Key, StringComparer.Ordinal)) {
            var values = new List<OptionEnumValue>();
            foreach (var value in member.Value["values"].Items) {
                var name = value["name"].AsString();
                if (name is null) {
                    continue;
                }

                values.Add(new OptionEnumValue(name, Naming.Pascal(name), value["summary"].AsString() ?? string.Empty));
            }

            var aliases = member.Value["valueAliases"].Members
                .Select(static a => new KeyValuePair<string, string>(a.Key, a.Value.AsString() ?? string.Empty))
                .OrderBy(static a => a.Key, StringComparer.Ordinal)
                .ToList();

            enums.Add(new OptionEnum(member.Key, values, aliases));
        }

        var options = new List<OptionEntry>();
        foreach (var item in root["options"].Items) {
            var key = item["key"].AsString();
            if (key is null) {
                continue;
            }

            var type = item["type"].AsString() ?? "string";
            var kind = type switch {
                "bool" => OptionValueKind.Bool,
                "int" => OptionValueKind.Int,
                _ when type.StartsWith("enum:", StringComparison.Ordinal) => OptionValueKind.Enum,
                _ when type.StartsWith("flags:", StringComparison.Ordinal) => OptionValueKind.Flags,
                _ => OptionValueKind.String
            };

            options.Add(
                new OptionEntry(
                    key,
                    item["aliases"].AsStringList(),
                    item["export"].AsStringList(),
                    item["language"].AsString() ?? "any",
                    kind,
                    kind switch {
                        OptionValueKind.Enum => type.Substring("enum:".Length),
                        OptionValueKind.Flags => type.Substring("flags:".Length),
                        _ => null
                    },
                    item["default"].IsNull ? null : item["default"].AsString(),
                    item["defaultSource"].AsString() ?? "unknown",
                    item["tier"].AsString() ?? "D",
                    item["construct"].AsString() ?? "Other",
                    item["summary"].AsString() ?? string.Empty,
                    item["since"].AsString() ?? "0.1",
                    item["oracle"].IsNull ? null : item["oracle"].AsString(),
                    item["docs"].IsNull ? null : item["docs"].AsString(),
                    item["templateLine"].IsNull ? null : item["templateLine"].AsInt(),
                    item["severitySuffix"].AsBool(),
                    item["inert"].IsNull ? null : item["inert"].AsString(),
                    item["expands"].AsStringList(),
                    item["min"].IsNull ? null : item["min"].AsInt(),
                    item["max"].IsNull ? null : item["max"].AsInt(),
                    item["boundsBecause"].IsNull ? null : item["boundsBecause"].AsString(),
                    item["tabMeans"].IsNull ? null : item["tabMeans"].AsString(),
                    item["freeFormBecause"].IsNull ? null : item["freeFormBecause"].AsString(),
                    item["unsweptBecause"].IsNull ? null : item["unsweptBecause"].AsString()
                )
            );
        }

        // ⚠ Dense and stable: ids are assigned by ordinal key order so that adding an option does
        // not renumber the ones after it (docs/plan/03 § "The option registry").
        options.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return new(enums, options);
    }

    public static string Literal(string? value) =>
        value is null
            ? "null"
            : "\""
            + value.Replace("""\""", """\\""")
                .Replace("\"", "\\\"")
                .Replace("\n", """\n""")
                .Replace(
                    "\r",
                    """\r"""
                )
            + "\"";

    public static string IntLiteral(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
}
