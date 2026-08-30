using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Options.Generator;

/// <summary>
///     Reads <c>options.json</c> and emits the option model: the <c>OptionId</c> enum, the option
///     enums, the registry metadata and key parser, and <c>FormattingOptions</c>.
/// </summary>
/// <remarks>
///     docs/plan/03-configuration-model.md § "The option registry". This runs as an incremental
///     generator rather than as a checked-in codegen step so that editing options.json is a build,
///     not a ritual.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class OptionsGenerator : IIncrementalGenerator {
    const string FileName = "options.json";
    const string Namespace = "Rikarin.Skala.Options";

    static readonly DiagnosticDescriptor MissingRegistry = new(
        "SKG001",
        "The option registry is missing",
        "No AdditionalFile named '{0}' was found; the option model cannot be generated",
        "Skala.Options",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor UnreadableRegistry = new(
        "SKG002",
        "The option registry could not be read",
        "'{0}' could not be read: {1}",
        "Skala.Options",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor DefaultOutOfDomain = new(
        "SKG003",
        "An option's default is not one of its values",
        "'{0}' declares type '{1}' but its default '{2}' is not in that domain ({3})",
        "Skala.Options",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A default outside the option's domain would be silently replaced by the first enum member, which is a different style than the one configured."
    );

    static readonly DiagnosticDescriptor DefaultOutOfRange = new(
        "SKG004",
        "An option's default is outside its own bounds",
        "'{0}' declares {1} but its default is '{2}'",
        "Skala.Options",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A bound that refuses the option's own default refuses a configuration nobody wrote, and the message would name a fallback the tool would not accept back."
    );

    static readonly DiagnosticDescriptor UnjustifiedFreeForm = new(
        "SKG005",
        "A free-form option carries no reason, or a closed one carries a reason",
        "{0}",
        "Skala.Options",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "`type: string` claims every string is legal. 27 options that are really enums carried that claim with no reason behind it and therefore validated nothing; `freeFormBecause` is what makes the claim reviewable."
    );

    /// <summary>⚠ Named rather than inline: a bare id bypasses doc 08's register (ADR-012).</summary>
    const string DuplicateAliasId = "SK9004";

    static readonly DiagnosticDescriptor DuplicateAlias = new(
        DuplicateAliasId,
        "Duplicate option alias",
        "'{0}' names more than one option ({1}); a second name for an option is a second thing to keep in sync",
        "Skala.Options",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "docs/plan/02-repository-layout.md § \"Naming\"."
    );

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var registry = context.AdditionalTextsProvider
            .Where(static text => text.Path.EndsWith(FileName, StringComparison.OrdinalIgnoreCase))
            .Select(static (text, token) => (text.Path, Content: text.GetText(token)?.ToString()))
            .Collect();

        context.RegisterSourceOutput(registry, static (production, files) => Emit(production, files));
    }

    static void Emit(SourceProductionContext context, ImmutableArray<(string Path, string? Content)> files) {
        if (files.Length == 0) {
            context.ReportDiagnostic(Diagnostic.Create(MissingRegistry, Location.None, FileName));
            return;
        }

        var (path, content) = files[0];
        if (content is null) {
            context.ReportDiagnostic(Diagnostic.Create(UnreadableRegistry, Location.None, path, "the file is empty"));
            return;
        }

        OptionRegistry model;
        try {
            model = OptionRegistryReader.Read(content);
        } catch (Exception exception) {
            context.ReportDiagnostic(Diagnostic.Create(UnreadableRegistry, Location.None, path, exception.Message));
            return;
        }

        if (!CheckSpellings(context, model) || !CheckDefaults(context, model) || !CheckDomains(context, model)) {
            return;
        }

        context.AddSource("OptionId.g.cs", SourceText.From(EmitOptionId(model), Encoding.UTF8));
        context.AddSource("OptionEnums.g.cs", SourceText.From(EmitEnums(model), Encoding.UTF8));
        context.AddSource("OptionRegistry.g.cs", SourceText.From(EmitRegistry(model), Encoding.UTF8));
        context.AddSource("FormattingOptions.g.cs", SourceText.From(EmitFormattingOptions(model), Encoding.UTF8));
    }

    static bool CheckSpellings(SourceProductionContext context, OptionRegistry model) {
        var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var option in model.Options) {
            foreach (var spelling in new[] { option.Key }.Concat(option.Aliases)) {
                if (!owners.TryGetValue(spelling, out var list)) {
                    owners[spelling] = list = [];
                }

                list.Add(option.Key);
            }
        }

        var clean = true;
        foreach (var pair in owners.Where(static p => p.Value.Count > 1)
                     .OrderBy(
                         static p => p.Key,
                         StringComparer.Ordinal
                     )) {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DuplicateAlias,
                    Location.None,
                    pair.Key,
                    string.Join(", ", pair.Value.OrderBy(static k => k, StringComparer.Ordinal))
                )
            );
            clean = false;
        }

        return clean;
    }

    static bool CheckDefaults(SourceProductionContext context, OptionRegistry model) {
        var clean = true;
        foreach (var option in model.Options) {
            if (option.Kind is not (OptionValueKind.Enum or OptionValueKind.Flags) || option.Default is null) {
                continue;
            }

            var declared = model.Enums.FirstOrDefault(e => string.Equals(
                    e.Name,
                    option.EnumName,
                    StringComparison.Ordinal
                )
            );
            var text = option.Default;
            if (option.SeveritySuffix) {
                var colon = text.LastIndexOf(':');
                if (colon >= 0) {
                    text = text.Substring(0, colon).Trim();
                }
            }

            var parts = option.Kind == OptionValueKind.Flags
                ? text.Split(',').Select(static part => part.Trim()).Where(static part => part.Length > 0).ToArray()
                : [text];

            if (declared is not null
                && parts.All(part =>
                    IndexOfValue(declared, part) >= 0
                    || declared.ValueAliases.Any(a => string.Equals(a.Key, part, StringComparison.Ordinal))
                )) {
                continue;
            }

            var domain = declared is null
                ? "no such enum"
                : string.Join(", ", declared.Values.Select(static v => v.EditorConfigName));
            context.ReportDiagnostic(
                Diagnostic.Create(DefaultOutOfDomain, Location.None, option.Key, option.EnumName, text, domain)
            );
            clean = false;
        }

        return clean;
    }

    /// <summary>
    ///     The bounds are consistent with the defaults, and every free-form entry says why it is one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both halves guard the same hole from opposite sides. An <c>int</c> with no bounds accepted
    ///     <c>max_line_length = -1</c> and <c>indent_size = 0</c>; a <c>string</c> with no reason
    ///     accepted <c>resharper_align_ternary = sideways</c>. The build is where that has to fail,
    ///     because a registry entry is added by hand and reviewed once.
    /// </remarks>
    static bool CheckDomains(SourceProductionContext context, OptionRegistry model) {
        var clean = true;
        foreach (var option in model.Options) {
            if (option.Kind == OptionValueKind.Int
                && int.TryParse(option.Default, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && (option.Min is { } min && number < min || option.Max is { } max && number > max)) {
                var bounds = option.Max is null
                    ? $"min {option.Min}"
                    : option.Min is null
                        ? $"max {option.Max}"
                        : $"min {option.Min} and max {option.Max}";
                context.ReportDiagnostic(
                    Diagnostic.Create(DefaultOutOfRange, Location.None, option.Key, bounds, option.Default)
                );
                clean = false;
            }

            if (option.Kind is not OptionValueKind.Int && (option.Min is not null || option.Max is not null)) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        UnjustifiedFreeForm,
                        Location.None,
                        $"'{option.Key}' is not an int but declares min/max"
                    )
                );
                clean = false;
            }

            var bounded = option.Min is not null || option.Max is not null;
            if (bounded != (option.BoundsBecause is { Length: > 0 })) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        UnjustifiedFreeForm,
                        Location.None,
                        bounded
                            ? $"'{option.Key}' declares a bound and no `boundsBecause`. A bound with no reason is the same guess the missing bound was."
                            : $"'{option.Key}' carries `boundsBecause` and declares no bound."
                    )
                );
                clean = false;
            }

            var freeForm = option.Kind == OptionValueKind.String;
            if (freeForm != (option.FreeFormBecause is { Length: > 0 })) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        UnjustifiedFreeForm,
                        Location.None,
                        freeForm
                            ? $"'{option.Key}' is type 'string' — every value is legal — and gives no `freeFormBecause`. Either name the domain as an enum, or record why there is none."
                            : $"'{option.Key}' is not a free-form string but carries `freeFormBecause`."
                    )
                );
                clean = false;
            }

            if (option.TabMeans is { } target
                && !model.Options.Any(other =>
                    string.Equals(other.Key, target, StringComparison.Ordinal)
                    && other.Kind == OptionValueKind.Int
                )) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        UnjustifiedFreeForm,
                        Location.None,
                        $"'{option.Key}' says `tab` means '{target}', which is not an int option."
                    )
                );
                clean = false;
            }
        }

        return clean;
    }

    static string Header() =>
        """
        // <auto-generated>
        //     Generated by Rikarin.Skala.Options.Generator from options.json. Do not edit.
        // </auto-generated>
        #nullable enable

        """;

    static string EmitOptionId(OptionRegistry model) {
        var builder = new StringBuilder(Header());
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine(
            "/// <summary>A dense, stable identifier for one style option. Ids are assigned in ordinal key order.</summary>"
        );
        builder.AppendLine("public enum OptionId {");
        for (var i = 0; i < model.Options.Count; i++) {
            var option = model.Options[i];
            builder.AppendLine($"    /// <summary><c>{Xml(option.Key)}</c> — {Xml(option.Summary)}</summary>");
            builder.AppendLine($"    {option.MemberName} = {i.ToString(CultureInfo.InvariantCulture)},");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    static string EmitEnums(OptionRegistry model) {
        var builder = new StringBuilder(Header());
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine($"namespace {Namespace};");
        foreach (var option in model.Enums) {
            builder.AppendLine();
            builder.AppendLine($"public enum {option.Name} {{");
            for (var i = 0; i < option.Values.Count; i++) {
                var value = option.Values[i];
                if (value.Summary.Length > 0) {
                    builder.AppendLine($"    /// <summary>{Xml(value.Summary)}</summary>");
                }

                builder.AppendLine($"    /// <remarks><c>{Xml(value.EditorConfigName)}</c></remarks>");
                builder.AppendLine($"    {value.MemberName} = {i.ToString(CultureInfo.InvariantCulture)},");
            }

            builder.AppendLine("}");
        }

        builder.AppendLine();
        builder.AppendLine(
            "/// <summary>Text to value, for every option enum, including ReSharper's value aliases.</summary>"
        );
        builder.AppendLine("public static class OptionEnums {");
        builder.AppendLine("    public static bool TryParse(string enumName, string text, out int value) {");
        builder.AppendLine("        switch (enumName) {");
        foreach (var option in model.Enums) {
            builder.AppendLine($"            case \"{option.Name}\":");
            builder.AppendLine("                switch (text) {");
            for (var i = 0; i < option.Values.Count; i++) {
                builder.AppendLine(
                    $"                    case {Lit(option.Values[i].EditorConfigName)}: value = {i.ToString(CultureInfo.InvariantCulture)}; return true;"
                );
            }

            foreach (var alias in option.ValueAliases) {
                var index = IndexOfValue(option, alias.Value);
                if (index >= 0) {
                    builder.AppendLine($"                    // ReSharper alias: {alias.Key} == {alias.Value}");
                    builder.AppendLine(
                        $"                    case {Lit(alias.Key)}: value = {index.ToString(CultureInfo.InvariantCulture)}; return true;"
                    );
                }
            }

            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                break;");
        }

        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        value = 0;");
        builder.AppendLine("        return false;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static string ToText(string enumName, int value) {");
        builder.AppendLine("        switch (enumName) {");
        foreach (var option in model.Enums) {
            builder.AppendLine($"            case \"{option.Name}\":");
            builder.AppendLine("                switch (value) {");
            for (var i = 0; i < option.Values.Count; i++) {
                builder.AppendLine(
                    $"                    case {i.ToString(CultureInfo.InvariantCulture)}: return {Lit(option.Values[i].EditorConfigName)};"
                );
            }

            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                break;");
        }

        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static string[] ValuesOf(string enumName) => enumName switch {");
        foreach (var option in model.Enums) {
            var values = string.Join(", ", option.Values.Select(static v => Lit(v.EditorConfigName)));
            builder.AppendLine($"        \"{option.Name}\" => [{values}],");
        }

        builder.AppendLine("        _ => []");
        builder.AppendLine("    };");
        builder.AppendLine();
        builder.AppendLine(
            "    /// <summary>ReSharper's alternative spellings for this enum's values — <c>true</c> for <c>always</c>, and the like. Part of the declared domain: <see cref=\"TryParse\"/> accepts them, so a test that sweeps `ValuesOf` alone sweeps less than the option accepts.</summary>"
        );
        builder.AppendLine("    public static string[] AliasesOf(string enumName) => enumName switch {");
        foreach (var option in model.Enums) {
            var aliases = string.Join(
                ", ",
                option.ValueAliases.Where(alias => IndexOfValue(option, alias.Value) >= 0)
                    .Select(static alias => Lit(alias.Key))
            );
            builder.AppendLine($"        \"{option.Name}\" => [{aliases}],");
        }

        builder.AppendLine("        _ => []");
        builder.AppendLine("    };");
        builder.AppendLine("}");
        return builder.ToString();
    }

    static int IndexOfValue(OptionEnum option, string editorConfigName) {
        for (var i = 0; i < option.Values.Count; i++) {
            if (string.Equals(option.Values[i].EditorConfigName, editorConfigName, StringComparison.Ordinal)) {
                return i;
            }
        }

        return -1;
    }

    static string EmitRegistry(OptionRegistry model) {
        var builder = new StringBuilder(Header());
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Collections.Frozen;");
        builder.AppendLine();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine("public enum OptionValueKind { Bool, Int, String, Enum, Flags }");
        builder.AppendLine();
        builder.AppendLine("/// <summary>docs/plan/03-configuration-model.md § \"Four tiers\".</summary>");
        builder.AppendLine("public enum OptionTier {");
        builder.AppendLine("    /// <summary>Implemented, and pinned by at least one oracle fixture.</summary>");
        builder.AppendLine("    A,");
        builder.AppendLine(
            "    /// <summary>Implemented, with a documented divergence in stated edge cases.</summary>"
        );
        builder.AppendLine("    B,");
        builder.AppendLine("    /// <summary>Parsed, validated, and deliberately not implemented.</summary>");
        builder.AppendLine("    C,");
        builder.AppendLine("    /// <summary>Known to the registry, not implemented yet.</summary>");
        builder.AppendLine("    D,");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(
            "/// <summary>Where an option's recorded default came from. distill may only drop a default that was checked against ReSharper.</summary>"
        );
        builder.AppendLine("public enum OptionDefaultSource {");
        builder.AppendLine(
            "    /// <summary>Verified against JetBrains' published EditorConfig property tables.</summary>"
        );
        builder.AppendLine("    ReSharperDocs,");
        builder.AppendLine(
            "    /// <summary>Derived by running the oracle under a configuration carrying nothing but root = true, and comparing against the fixture that exercises the option. A strong signal rather than proof, because options interact.</summary>"
        );
        builder.AppendLine("    OracleProbe,");
        builder.AppendLine(
            "    /// <summary>The value the Rider export holds. Rider's default for most keys, the author's choice for the rest, with nothing distinguishing the two.</summary>"
        );
        builder.AppendLine("    Template,");
        builder.AppendLine("    /// <summary>No basis beyond the export.</summary>");
        builder.AppendLine("    Unknown,");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("public sealed record OptionInfo(");
        builder.AppendLine("    OptionId Id,");
        builder.AppendLine("    string Key,");
        builder.AppendLine("    IReadOnlyList<string> Aliases,");
        builder.AppendLine("    string Language,");
        builder.AppendLine("    OptionValueKind Kind,");
        builder.AppendLine("    string? EnumName,");
        builder.AppendLine("    string? Default,");
        builder.AppendLine("    OptionDefaultSource DefaultSource,");
        builder.AppendLine("    OptionTier Tier,");
        builder.AppendLine("    string Construct,");
        builder.AppendLine("    string Summary,");
        builder.AppendLine("    string Since,");
        builder.AppendLine("    string? Oracle,");
        builder.AppendLine("    string? Docs,");
        builder.AppendLine("    int? TemplateLine,");
        builder.AppendLine("    bool SeveritySuffix,");
        builder.AppendLine(
            "    /// <summary>Why this option can never be observed, or null when it can. ⚠ An inert option is Tier D but is NOT a gap: no input distinguishes its values, because another rule wins by the documented ordering or because the writer cannot produce the shape it governs. docs/plan/05 records each one and the reason. Reporting these as unimplemented makes the coverage number noise; omitting them from the report entirely hides that the configuration set them.</summary>"
        );
        builder.AppendLine("    string? Inert,");
        builder.AppendLine("    IReadOnlyList<OptionId> Expands,");
        builder.AppendLine(
            "    /// <summary>The smallest value an <c>Int</c> option accepts, inclusive, or null where no lower bound is knowable. ⚠ Read with the consumer's clamping: `max_line_length = 0` and `max_*_on_line = 0` are supported values, so those minimums are 0.</summary>"
        );
        builder.AppendLine("    int? Min,");
        builder.AppendLine(
            "    /// <summary>The largest value an <c>Int</c> option accepts, inclusive. Almost always null: an upper bound nobody can justify refuses values for no reason.</summary>"
        );
        builder.AppendLine("    int? Max,");
        builder.AppendLine(
            "    /// <summary>The option whose value the literal <c>tab</c> stands for, per the EditorConfig specification's <c>indent_size</c>. Resolved by <c>OptionResolver</c>, which can see the rest of the configuration; <c>TrySet</c> cannot.</summary>"
        );
        builder.AppendLine("    OptionId? TabMeans,");
        builder.AppendLine(
            "    /// <summary>Why a <c>String</c> option has no closed domain, and null for every other kind. ⚠ A string kind is a claim that every string is legal, and 27 options that are really enums carried it without one. The reason is what a reviewer can disagree with.</summary>"
        );
        builder.AppendLine("    string? FreeFormBecause,");
        builder.AppendLine(
            "    /// <summary>Why this entry names no <c>Oracle</c> fixture, when <see cref=\"Inert\"/> is not the answer. ⚠ The two are different claims and must not be merged: <c>Inert</c> says no input distinguishes the option's values, which is a fact about the world; this says the SWEEP cannot ask, which is usually a fact about Skala — the oracle separates the values and Skala is flat at every one of them, so a glob would file an <c>INERT</c> row the registry never declared, on a fixture Skala cannot reproduce. It also carries the measurement behind a Tier C refusal, where <c>Inert</c> is forbidden by construction.</summary>"
        );
        builder.AppendLine("    string? UnsweptBecause);");
        builder.AppendLine();
        builder.AppendLine("public static class OptionRegistry {");
        builder.AppendLine(
            $"    public const int Count = {model.Options.Count.ToString(CultureInfo.InvariantCulture)};"
        );
        builder.AppendLine();
        builder.AppendLine("    static readonly OptionInfo[] Table = CreateTable();");
        builder.AppendLine("    static readonly FrozenDictionary<string, OptionId> BySpelling = CreateIndex();");
        builder.AppendLine();
        builder.AppendLine("    public static IReadOnlyList<OptionInfo> All => Table;");
        builder.AppendLine();
        builder.AppendLine("    public static OptionInfo Get(OptionId id) => Table[(int)id];");
        builder.AppendLine();
        builder.AppendLine(
            "    /// <summary>Resolves any spelling ReSharper accepts — canonical key or alias — to its option.</summary>"
        );
        builder.AppendLine(
            "    public static bool TryResolve(string key, out OptionId id) => BySpelling.TryGetValue(key, out id);"
        );
        builder.AppendLine();
        builder.AppendLine("    public static IReadOnlyCollection<string> Spellings => BySpelling.Keys;");
        builder.AppendLine();
        builder.AppendLine("    static FrozenDictionary<string, OptionId> CreateIndex() {");
        builder.AppendLine("        var index = new Dictionary<string, OptionId>(StringComparer.Ordinal);");
        builder.AppendLine("        foreach (var info in Table) {");
        builder.AppendLine("            index[info.Key] = info.Id;");
        builder.AppendLine("            foreach (var alias in info.Aliases) {");
        builder.AppendLine("                index[alias] = info.Id;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return index.ToFrozenDictionary(StringComparer.Ordinal);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    static OptionInfo[] CreateTable() => [");
        foreach (var option in model.Options) {
            var aliases = option.Aliases.Count == 0
                ? "[]"
                : "[" + string.Join(", ", option.Aliases.Select(static a => OptionRegistryReader.Literal(a))) + "]";
            var expands = option.Expands.Count == 0
                ? "[]"
                : "[" + string.Join(", ", option.Expands.Select(e => "OptionId." + Naming.Pascal(e))) + "]";
            builder.AppendLine(
                $"        new(OptionId.{option.MemberName}, {OptionRegistryReader.Literal(option.Key)}, {aliases}, "
                + $"{OptionRegistryReader.Literal(option.Language)}, OptionValueKind.{option.Kind}, "
                + $"{OptionRegistryReader.Literal(option.EnumName)}, {OptionRegistryReader.Literal(option.Default)}, "
                + $"OptionDefaultSource.{DefaultSourceMember(option.DefaultSource)}, OptionTier.{option.Tier}, "
                + $"{OptionRegistryReader.Literal(option.Construct)}, {OptionRegistryReader.Literal(option.Summary)}, "
                + $"{OptionRegistryReader.Literal(option.Since)}, {OptionRegistryReader.Literal(option.Oracle)}, "
                + $"{OptionRegistryReader.Literal(option.Docs)}, {OptionRegistryReader.IntLiteral(option.TemplateLine)}, "
                + $"{(option.SeveritySuffix ? "true" : "false")}, "
                + $"{OptionRegistryReader.Literal(option.Inert)}, {expands}, "
                + $"{OptionRegistryReader.IntLiteral(option.Min)}, {OptionRegistryReader.IntLiteral(option.Max)}, "
                + $"{(option.TabMeans is null ? "null" : "OptionId." + Naming.Pascal(option.TabMeans))}, "
                + $"{OptionRegistryReader.Literal(option.FreeFormBecause)}, "
                + $"{OptionRegistryReader.Literal(option.UnsweptBecause)}),"
            );
        }

        builder.AppendLine("    ];");
        builder.AppendLine("}");
        return builder.ToString();
    }

    static string DefaultSourceMember(string value) =>
        value switch {
            "resharper-docs" => "ReSharperDocs",
            "oracle-probe" => "OracleProbe",
            "template" => "Template",
            _ => "Unknown"
        };

    static string EmitFormattingOptions(OptionRegistry model) {
        var builder = new StringBuilder(Header());
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine(
            "// Every group struct carries both arrays so that the shape is uniform, even where the group"
        );
        builder.AppendLine("// happens to hold no option of one kind.");
        builder.AppendLine("#pragma warning disable CS9113");
        builder.AppendLine();
        builder.AppendLine($"namespace {Namespace};");
        builder.AppendLine();
        builder.AppendLine(
            """
            /// <summary>
            /// The resolved style options for one file, as a struct of arrays keyed by <see cref="OptionId"/>.
            /// </summary>
            /// <remarks>
            /// ⚠ Reading an option is an array index, never a dictionary lookup. The fitting pass reads
            /// options tens of millions of times over the corpus and a dictionary there is a 3–4× slowdown
            /// on the whole operation (docs/plan/13-performance.md § "The fitting pass").
            /// </remarks>
            """
        );
        builder.AppendLine("public readonly struct FormattingOptions {");
        builder.AppendLine("    readonly int[] _scalars;");
        builder.AppendLine("    readonly string?[] _strings;");
        builder.AppendLine();
        builder.AppendLine("    internal FormattingOptions(int[] scalars, string?[] strings) {");
        builder.AppendLine("        _scalars = scalars;");
        builder.AppendLine("        _strings = strings;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine(
            "    public static FormattingOptions Defaults { get; } = new FormattingOptionsBuilder().Build();"
        );
        builder.AppendLine();
        builder.AppendLine("    public bool GetBool(OptionId id) => _scalars[(int)id] != 0;");
        builder.AppendLine();
        builder.AppendLine("    public int GetInt(OptionId id) => _scalars[(int)id];");
        builder.AppendLine();
        builder.AppendLine("    public int GetRaw(OptionId id) => _scalars[(int)id];");
        builder.AppendLine();
        builder.AppendLine("    public string? GetString(OptionId id) => _strings[(int)id];");
        builder.AppendLine();
        builder.AppendLine(
            "    /// <summary>The option's value in its .editorconfig spelling, for reporting.</summary>"
        );
        builder.AppendLine("    public string GetText(OptionId id) {");
        builder.AppendLine("        var info = OptionRegistry.Get(id);");
        builder.AppendLine("        return info.Kind switch {");
        builder.AppendLine("            OptionValueKind.Bool => _scalars[(int)id] != 0 ? \"true\" : \"false\",");
        builder.AppendLine(
            "            OptionValueKind.Int => _scalars[(int)id].ToString(System.Globalization.CultureInfo.InvariantCulture),"
        );
        builder.AppendLine(
            "            OptionValueKind.Enum => OptionEnums.ToText(info.EnumName!, _scalars[(int)id]),"
        );
        builder.AppendLine("            OptionValueKind.Flags => _strings[(int)id] ?? string.Empty,");
        builder.AppendLine("            _ => _strings[(int)id] ?? string.Empty");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        EmitGroups(builder, model);
        builder.AppendLine("}");
        builder.AppendLine();
        EmitBuilder(builder, model);
        return builder.ToString();
    }

    static void EmitGroups(StringBuilder builder, OptionRegistry model) {
        var roots = model.Options
            .GroupBy(static o => o.GroupPath[0], StringComparer.Ordinal)
            .OrderBy(static g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var root in roots) {
            builder.AppendLine($"    public {root.Key}Group {root.Key} => new(_scalars, _strings);");
        }

        foreach (var root in roots) {
            var direct = root.Where(static o => o.GroupPath.Count == 1).ToList();
            var nested = root.Where(static o => o.GroupPath.Count > 1)
                .GroupBy(static o => o.GroupPath[1], StringComparer.Ordinal)
                .OrderBy(static g => g.Key, StringComparer.Ordinal)
                .ToList();

            builder.AppendLine();
            builder.AppendLine($"    public readonly struct {root.Key}Group(int[] scalars, string?[] strings) {{");
            foreach (var child in nested) {
                builder.AppendLine($"        public {root.Key}{child.Key}Group {child.Key} => new(scalars, strings);");
            }

            EmitAccessors(builder, direct, "        ");
            builder.AppendLine("    }");

            foreach (var child in nested) {
                builder.AppendLine();
                builder.AppendLine(
                    $"    public readonly struct {root.Key}{child.Key}Group(int[] scalars, string?[] strings) {{"
                );
                EmitAccessors(builder, [.. child], "        ");
                builder.AppendLine("    }");
            }
        }
    }

    static void EmitAccessors(StringBuilder builder, IReadOnlyList<OptionEntry> options, string indent) {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var collisions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options) {
            if (!used.Add(Naming.LeafName(option.Key))) {
                collisions.Add(Naming.LeafName(option.Key));
            }
        }

        foreach (var option in options.OrderBy(static o => o.Key, StringComparer.Ordinal)) {
            var leaf = Naming.LeafName(option.Key);
            var name = collisions.Contains(leaf) ? option.MemberName : leaf;
            var type = option.Kind switch {
                OptionValueKind.Bool => "bool",
                OptionValueKind.Int => "int",
                OptionValueKind.Enum => option.EnumName!,
                // Flags is a comma-separated subset of an enum's domain; there is no single member
                // to return, so it stays text and OptionEnums.ValuesOf names what is legal.
                _ => "string?"
            };

            var access = option.Kind switch {
                OptionValueKind.Bool => $"scalars[(int)OptionId.{option.MemberName}] != 0",
                OptionValueKind.Int => $"scalars[(int)OptionId.{option.MemberName}]",
                OptionValueKind.Enum => $"({option.EnumName})scalars[(int)OptionId.{option.MemberName}]",
                _ => $"strings[(int)OptionId.{option.MemberName}]"
            };

            builder.AppendLine(
                $"{indent}/// <summary><c>{Xml(option.Key)}</c> — {Xml(option.Summary)} (Tier {option.Tier})</summary>"
            );
            builder.AppendLine($"{indent}public {type} {name} => {access};");
        }
    }

    static void EmitBuilder(StringBuilder builder, OptionRegistry model) {
        builder.AppendLine(
            """
            /// <summary>Accumulates option values, starting from the registry's defaults.</summary>
            public sealed class FormattingOptionsBuilder {
                readonly int[] _scalars = CreateDefaultScalars();
                readonly string?[] _strings = CreateDefaultStrings();

                public FormattingOptions Build() => new((int[])_scalars.Clone(), (string?[])_strings.Clone());

                /// <summary>
                /// Applies one <c>.editorconfig</c> value. Returns false — with the reason — when the text is
                /// not in the option's domain; unknown configuration is a diagnostic, never a silent default.
                /// </summary>
                public bool TrySet(OptionId id, string text, out string? error) {
                    var info = OptionRegistry.Get(id);
                    var value = text.Trim();
                    if (info.SeveritySuffix) {
                        var colon = value.LastIndexOf(':');
                        if (colon >= 0) {
                            value = value.Substring(0, colon).Trim();
                        }
                    }

                    switch (info.Kind) {
                        case OptionValueKind.Bool:
                            switch (value) {
                                case "true":
                                case "always":
                                    _scalars[(int)id] = 1;
                                    error = null;
                                    return true;
                                case "false":
                                case "never":
                                    _scalars[(int)id] = 0;
                                    error = null;
                                    return true;
                                default:
                                    error = "expected true or false";
                                    return false;
                            }

                        case OptionValueKind.Int:
                            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number)) {
                                error = info.TabMeans is null
                                    ? "expected an integer" + Range(info)
                                    : "expected an integer" + Range(info) + ", or `tab` to take the width from " + OptionRegistry.Get(info.TabMeans.Value).Key;
                                return false;
                            }

                            // ⚠ An int with no domain is how `max_line_length = -1` and
                            // `indent_size = 0` reached the formatter. Both are then clamped —
                            // silently, to a width the user did not ask for.
                            if (info.Min is { } min && number < min || info.Max is { } max && number > max) {
                                error = "expected an integer" + Range(info);
                                return false;
                            }

                            _scalars[(int)id] = number;
                            error = null;
                            return true;

                        case OptionValueKind.Enum:
                            if (OptionEnums.TryParse(info.EnumName!, value, out var member)) {
                                _scalars[(int)id] = member;
                                error = null;
                                return true;
                            }

                            error = "expected one of " + string.Join(", ", OptionEnums.ValuesOf(info.EnumName!));
                            return false;

                        case OptionValueKind.Flags:
                            foreach (var part in value.Split(',')) {
                                var trimmed = part.Trim();
                                if (trimmed.Length == 0) {
                                    continue;
                                }

                                if (!OptionEnums.TryParse(info.EnumName!, trimmed, out _)) {
                                    error = "'" + trimmed + "' is not one of " + string.Join(", ", OptionEnums.ValuesOf(info.EnumName!));
                                    return false;
                                }
                            }

                            _strings[(int)id] = value;
                            error = null;
                            return true;

                        default:
                            _strings[(int)id] = value;
                            error = null;
                            return true;
                    }
                }

                /// <summary>The bounds an int option declares, as a phrase for the message.</summary>
                /// <remarks>
                /// ⚠ It names the range rather than saying "out of range". A message that does not say what
                /// would have been accepted leaves the reader guessing, and guessing is what produced the
                /// value being refused.
                /// </remarks>
                static string Range(OptionInfo info) =>
                    (info.Min, info.Max) switch {
                        (null, null) => "",
                        ({ } lower, null) => " >= " + lower.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        (null, { } upper) => " <= " + upper.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ({ } lower, { } upper) => " between " + lower.ToString(System.Globalization.CultureInfo.InvariantCulture) + " and " + upper.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    };
            """
        );

        builder.AppendLine();
        builder.AppendLine("    static int[] CreateDefaultScalars() {");
        builder.AppendLine(
            $"        var scalars = new int[{model.Options.Count.ToString(CultureInfo.InvariantCulture)}];"
        );
        foreach (var option in model.Options) {
            if (option.Default is null) {
                continue;
            }

            var value = DefaultScalar(model, option);
            if (value is not null and not "0") {
                builder.AppendLine($"        scalars[(int)OptionId.{option.MemberName}] = {value};");
            }
        }

        builder.AppendLine("        return scalars;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    static string?[] CreateDefaultStrings() {");
        builder.AppendLine(
            $"        var strings = new string?[{model.Options.Count.ToString(CultureInfo.InvariantCulture)}];"
        );
        foreach (var option in model.Options) {
            if (option.Kind is OptionValueKind.String or OptionValueKind.Flags && option.Default is not null) {
                builder.AppendLine(
                    $"        strings[(int)OptionId.{option.MemberName}] = {OptionRegistryReader.Literal(StripSeverity(option, option.Default))};"
                );
            }
        }

        builder.AppendLine("        return strings;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    /// <summary>
    ///     Microsoft's style keys are written <c>value:severity</c>. The severity configures a rule and
    ///     belongs to the rule layer; the option's value is what is left.
    /// </summary>
    static string StripSeverity(OptionEntry option, string text) {
        if (!option.SeveritySuffix) {
            return text;
        }

        var colon = text.LastIndexOf(':');
        return colon < 0 ? text : text.Substring(0, colon).Trim();
    }

    static string? DefaultScalar(OptionRegistry model, OptionEntry option) {
        var text = option.Default;
        if (text is null) {
            return null;
        }

        if (option.SeveritySuffix) {
            var colon = text.LastIndexOf(':');
            if (colon >= 0) {
                text = text.Substring(0, colon).Trim();
            }
        }

        switch (option.Kind) {
            case OptionValueKind.Bool:
                return text is "true" or "always" ? "1" : "0";
            case OptionValueKind.Int:
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : "0";
            case OptionValueKind.Enum:
                var declared = model.Enums.FirstOrDefault(e => string.Equals(
                        e.Name,
                        option.EnumName,
                        StringComparison.Ordinal
                    )
                );
                if (declared is null) {
                    return "0";
                }

                var index = IndexOfValue(declared, text);
                if (index < 0) {
                    foreach (var alias in declared.ValueAliases) {
                        if (string.Equals(alias.Key, text, StringComparison.Ordinal)) {
                            index = IndexOfValue(declared, alias.Value);
                            break;
                        }
                    }
                }

                return (index < 0 ? 0 : index).ToString(CultureInfo.InvariantCulture);
            default:
                return null;
        }
    }

    /// <summary>Escapes text for an XML documentation comment.</summary>
    static string Xml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\n", " ").Replace("\r", " ");

    /// <summary>Escapes text for a C# string literal, quotes included.</summary>
    static string Lit(string? value) => OptionRegistryReader.Literal(value);
}
