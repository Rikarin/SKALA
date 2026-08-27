using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis;

/// <summary>
/// <c>skala explain SK1010</c>, and the generator behind <c>docs/rules/</c>.
/// </summary>
/// <remarks>
/// ⚠ One source, three surfaces (docs/plan/08 § "Documentation"): the docs page, the
/// <c>explain</c> output and the SARIF <c>rules[]</c> block are all the same
/// <see cref="RuleInfo"/> rendered differently. A second copy of a rule's rationale is a second
/// copy to keep true.
/// <para>
/// ⚠ <c>explain</c> is what stops an agent from arguing with a rule or suppressing it. A model that
/// can read <em>why</em> a rule exists will restructure the code; one that only sees the message
/// will add a pragma.
/// </para>
/// </remarks>
public static class ExplainCommand {
    /// <summary>
    /// <c>skala explain &lt;ruleId | optionKey&gt;</c> — both halves of the configuration, one command.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The option half was documented and missing.</b> docs/plan/11 has always spelled the
    /// argument <c>&lt;ruleId | optionKey&gt;</c>, and until M9 every option key was answered with
    /// "is not a Skala rule" — <c>insert_final_newline</c>, <c>csharp_indent_case_contents</c>,
    /// <c>dotnet_sort_system_directives_first</c>, all of them. The two halves of what Skala reads
    /// are rules and options; a person looking at an `.editorconfig` line and asking what it does
    /// was sent to <c>config explain</c>, which takes a <em>file path</em> and dumps all
    /// <see cref="OptionRegistry.Count"/> effective options rather than answering the question.
    /// <para>
    /// Order matters: rules first, because <c>SK</c> ids are unambiguous and the option registry
    /// holds no key that looks like one.
    /// </para>
    /// </remarks>
    public static CommandResult Run(string ruleId) {
        if (RuleCatalog.Find(ruleId) is { } rule) {
            return new CommandResult(ExitCodes.Ok, Render(rule));
        }

        if (OptionRegistry.TryResolve(ruleId, out var optionId)) {
            return new CommandResult(ExitCodes.Ok, Render(OptionRegistry.Get(optionId)));
        }

        return new CommandResult(ExitCodes.ConfigurationError, NotFound(ruleId));
    }

    /// <summary>
    /// ⚠ Two namespaces, so two ways to be nearly right. A token shaped like a rule id gets rule
    /// suggestions by prefix; anything else is a candidate option key and gets the same
    /// edit-distance suggestion <c>config check</c> gives an unknown `.editorconfig` key, so the
    /// two surfaces answer a typo the same way.
    /// </summary>
    static string NotFound(string token) {
        var near = RuleCatalog.All
            .Where(candidate => candidate.Id.StartsWith(
                    token[..Math.Min(4, token.Length)],
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Select(static candidate => candidate.Id)
            .Take(5)
            .ToArray();

        if (near.Length > 0) {
            return $"skala explain: '{token}' is not a Skala rule. Did you mean "
                + string.Join(", ", near)
                + "?\n";
        }

        // ⚠ A token shaped like a rule id is answered as a rule even when nothing is near it.
        // Telling somebody who typed `SK9999` that it is "neither a rule nor an option" invites
        // them to go looking for the option, and `SK9xxx` ids exist — they are the tool's own
        // diagnostics rather than rules, which is the thing worth saying.
        if (IsRuleShaped(token)) {
            return $"skala explain: '{token}' is not a Skala rule. `skala rules` lists the {RuleCatalog.All.Count.ToString(CultureInfo.InvariantCulture)} allocated ids.\n";
        }

        var message = $"skala explain: '{token}' is neither a Skala rule nor a configuration option.";
        if (ConfigurationAnalyzer.DidYouMean(token) is { } spelling) {
            message += $" Did you mean `{spelling}`?";
        }

        return message + "\n";
    }

    static bool IsRuleShaped(string token) =>
        token.Length == 6
        && token.StartsWith("SK", StringComparison.OrdinalIgnoreCase)
        && token.AsSpan(2).ContainsAnyExceptInRange('0', '9') is false;

    /// <summary>
    /// An option's page: what it governs, what Skala does with it, and — the part that matters when
    /// somebody is reading a diff — where its default came from.
    /// </summary>
    public static string Render(OptionInfo option) {
        var builder = new StringBuilder();
        builder.Append(option.Key).Append("  ").AppendLine(option.Construct);
        builder.Append(new string('─', 78)).AppendLine();
        builder.AppendLine(Wrap(option.Summary, 78));
        builder.AppendLine();

        builder.Append("value  ").Append(option.EnumName is { } name ? name : option.Kind.ToString().ToLowerInvariant());
        if (option.Default is { } value) {
            builder.Append("  ·  default ").Append(value);
        }

        builder.Append("  ·  from ").Append(option.DefaultSource);
        builder.AppendLine();

        builder.Append("tier ").Append(option.Tier);
        builder.Append("  ·  ").Append(option.Language);
        builder.Append("  ·  since ").Append(option.Since);
        if (option.SeveritySuffix) {
            builder.Append("  ·  takes a `_severity` suffix");
        }

        builder.AppendLine();

        if (option.Aliases.Count > 0) {
            builder.AppendLine("also spelled: " + string.Join(", ", option.Aliases));
        }

        if (option.Expands.Count > 0) {
            builder.AppendLine(
                "expands to: " + string.Join(", ", option.Expands.Select(static id => OptionRegistry.Get(id).Key))
            );
        }

        // ⚠ The one thing a reader most needs and would otherwise learn by experiment. An inert
        // option is set in the file, accepted by `config check`, and changes no byte of output.
        if (option.Inert is { Length: > 0 } inert) {
            builder.AppendLine();
            builder.AppendLine("⚠ Skala's output does not depend on this option.");
            builder.AppendLine(Wrap(inert, 78));
        }

        if (option.Oracle is { Length: > 0 } oracle) {
            builder.AppendLine();
            builder.AppendLine("oracle: " + oracle);
        }

        if (option.Docs is { Length: > 0 } docs) {
            builder.AppendLine("docs: " + docs);
        }

        return builder.ToString();
    }

    /// <summary>The terminal form. Plain text, because it is read in a transcript as often as in a shell.</summary>
    public static string Render(RuleInfo rule) {
        var builder = new StringBuilder();
        builder.Append(rule.Id).Append("  ").AppendLine(rule.Title);
        builder.Append(new string('─', 78)).AppendLine();
        builder.AppendLine(rule.Summary);
        builder.AppendLine();
        builder.AppendLine(Wrap(rule.Rationale, 78));
        builder.AppendLine();

        if (rule.BadExample.Length > 0) {
            builder.AppendLine("Instead of:");
            builder.AppendLine(Indent(rule.BadExample));
            builder.AppendLine();
        }

        if (rule.GoodExample.Length > 0) {
            builder.AppendLine("Write:");
            builder.AppendLine(Indent(rule.GoodExample));
            builder.AppendLine();
        }

        if (rule.FalsePositives.Length > 0) {
            builder.AppendLine("When it does not fire:");
            builder.AppendLine(Wrap(rule.FalsePositives, 78));
            builder.AppendLine();
        }

        builder.Append("category ").Append(rule.Category);
        builder.Append("  ·  default ").Append(rule.DefaultSeverity.ToString().ToLowerInvariant());
        builder.Append("  ·  scope ").Append(rule.Scope.ToString().ToLowerInvariant());
        builder.Append(rule.HasFix ? rule.FixIsSafe ? "  ·  safe fix" : "  ·  fix (review it)" : "  ·  no fix");
        if (rule.LanguageVersion is { } floor) {
            builder.Append("  ·  C# ").Append(floor).Append('+');
        }

        builder.AppendLine();

        if (rule.Configuration.Count > 0) {
            builder.AppendLine("configuration: " + string.Join(", ", rule.Configuration));
        }

        if (rule.ReSharperSeverityKey is { } key) {
            builder.AppendLine("ReSharper: " + rule.ReSharperId + "  (" + key + ")");
        }

        if (rule.ReSharperNote is { Length: > 0 } note) {
            builder.AppendLine(Wrap(note, 78));
        }

        if (rule.Supersedes.Count > 0) {
            builder.AppendLine("supersedes: " + string.Join(", ", rule.Supersedes));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The markdown form, one page per rule, written to <c>docs/rules/</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Generated, never hand-edited: <c>RuleCatalogTests.DocsPages_AreUpToDate</c> regenerates and
    /// compares, so a rules.json change with no docs regeneration is a failing test rather than a
    /// documentation page that quietly describes the previous behaviour.
    /// </remarks>
    public static string RenderMarkdown(RuleInfo rule) {
        var builder = new StringBuilder();
        builder.Append("# ").Append(rule.Id).Append(" — ").AppendLine(rule.Title);
        builder.AppendLine();
        builder.AppendLine("<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->");
        builder.AppendLine();
        builder.AppendLine(rule.Summary);
        builder.AppendLine();
        builder.AppendLine("| | |");
        builder.AppendLine("|---|---|");
        builder.Append("| Category | ").Append(rule.Category).AppendLine(" |");
        builder.Append("| Default severity | ")
            .Append(rule.DefaultSeverity.ToString().ToLowerInvariant())
            .AppendLine(" |");
        builder.Append("| Scope | ").Append(rule.Scope).AppendLine(" |");
        builder.Append("| Needs a compilation | ").Append(rule.RequiresSemantics ? "yes" : "no").AppendLine(" |");
        builder.Append("| Fix | ")
            .Append(rule.HasFix ? rule.FixIsSafe ? "yes, safe" : "yes, review it" : "no")
            .AppendLine(" |");
        builder.Append("| Language version floor | ").Append(rule.LanguageVersion ?? "—").AppendLine(" |");
        builder.Append("| Since | ").Append(rule.Since).AppendLine(" |");
        if (rule.ReSharperId is { } resharper) {
            builder.Append("| ReSharper | `")
                .Append(resharper)
                .Append("` (`")
                .Append(rule.ReSharperSeverityKey)
                .AppendLine("`) |");
        }

        if (rule.Supersedes.Count > 0) {
            builder.Append("| Supersedes | ").Append(string.Join(", ", rule.Supersedes)).AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Why");
        builder.AppendLine();
        builder.AppendLine(rule.Rationale);
        builder.AppendLine();

        if (rule.BadExample.Length > 0 || rule.GoodExample.Length > 0) {
            builder.AppendLine("## Example");
            builder.AppendLine();
            if (rule.BadExample.Length > 0) {
                builder.AppendLine("Instead of:");
                builder.AppendLine();
                builder.AppendLine("```csharp");
                builder.AppendLine(rule.BadExample);
                builder.AppendLine("```");
                builder.AppendLine();
            }

            if (rule.GoodExample.Length > 0) {
                builder.AppendLine("Write:");
                builder.AppendLine();
                builder.AppendLine("```csharp");
                builder.AppendLine(rule.GoodExample);
                builder.AppendLine("```");
                builder.AppendLine();
            }
        }

        if (rule.ReSharperNote is { Length: > 0 } note) {
            builder.AppendLine("## The ReSharper mapping");
            builder.AppendLine();
            builder.AppendLine(note);
            builder.AppendLine();
        }

        builder.AppendLine("## When it does not fire");
        builder.AppendLine();
        builder.AppendLine(rule.FalsePositives);
        builder.AppendLine();

        if (rule.Configuration.Count > 0) {
            builder.AppendLine("## Configuration");
            builder.AppendLine();
            foreach (var key in rule.Configuration) {
                builder.Append("- `").Append(key).AppendLine("`");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>The index page, so that `docs/rules/` is browsable rather than a pile of files.</summary>
    public static string RenderIndex() {
        var builder = new StringBuilder();
        builder.AppendLine("# Rules");
        builder.AppendLine();
        builder.AppendLine("<!-- Generated from Rules/Rikarin.Skala.Rules.Metadata/rules.json. Do not edit. -->");
        builder.AppendLine();
        builder.Append("`SK` + four digits, allocated once and never re-purposed (ADR-012). ")
            .Append(RuleCatalog.All.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" ids are allocated.");
        builder.AppendLine();

        foreach (var category in RuleCatalog.All.GroupBy(static rule => rule.Category, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            builder.Append("## ").AppendLine(category.Key);
            builder.AppendLine();
            builder.AppendLine("| Id | Rule | Severity | Fix | Loose mode |");
            builder.AppendLine("|---|---|---|---|---|");
            foreach (var rule in category.OrderBy(static rule => rule.Id, StringComparer.Ordinal)) {
                builder.Append("| [")
                    .Append(rule.Id)
                    .Append("](")
                    .Append(rule.Id)
                    .Append(".md) | ")
                    .Append(rule.Title.Replace("|", "\\|", StringComparison.Ordinal))
                    .Append(" | ")
                    .Append(rule.DefaultSeverity.ToString().ToLowerInvariant())
                    .Append(" | ")
                    .Append(rule.HasFix ? rule.FixIsSafe ? "safe" : "review" : "—")
                    .Append(" | ")
                    .Append(rule.RunsWithoutAProject ? "yes" : "no")
                    .AppendLine(" |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    static string Indent(string text) => string.Join("\n", text.Split('\n').Select(static line => "    " + line));

    static string Wrap(string text, int width) {
        var builder = new StringBuilder();
        var column = 0;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if (column > 0 && column + word.Length + 1 > width) {
                builder.AppendLine();
                column = 0;
            } else if (column > 0) {
                builder.Append(' ');
                column++;
            }

            builder.Append(word);
            column += word.Length;
        }

        return builder.ToString();
    }
}
