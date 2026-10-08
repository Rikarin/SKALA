using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Core.Diagnostics;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
///     What every loaded tree's <see cref="DocumentationMode" /> is, and what the run says when the
///     project did not ask the compiler for documentation (#384, #388).
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         A project without <c>GenerateDocumentationFile</c> compiles with
///         <see cref="DocumentationMode.None" />, and under it a <c>///</c> comment is ordinary trivia.
///     </b> Every rule that reads documentation then sees none: measured under <c>--load=binlog</c>,
///     <c>SK7010</c> reported every documented member of a two-member probe as undocumented and
///     <c>SK7100</c> found nothing, while the same source with the property on — and the same source
///     under <c>--load=loose</c>, which always parsed documentation — gave the right answer. A non-zero
///     from a disabled instrument looks exactly like real work.
///     <para>
///         ⚠ <b>Parse, never Diagnose.</b> <see cref="DocumentationMode.Parse" /> gives documentation
///         its structure and reports nothing about it; the loose loader, the formatter and the rule
///         fixture harness have always parsed this way, so the rules are specified in this mode and
///         only the project-backed loads disagreed. Diagnose would put <c>CS1591</c> and its family
///         into a report for a project that never asked for them. A project that did ask keeps
///         <see cref="DocumentationMode.Diagnose" /> untouched. ⚠ One compiler diagnostic does move:
///         the hidden <c>CS8019</c>, which the compiler withholds in a <c>None</c> tree because it
///         cannot know whether a <c>cref</c> needed the import — and which <c>UsingsRule</c> reads, so
///         a binlog arrangement could not remove an unused using until this existed.
///     </para>
///     <para>
///         ⚠ Refusing the documentation rules instead was considered and rejected. The verdicts that
///         move are not only the three rules #388 named: <c>SK1110</c> declined every forwarding
///         overload carrying a doc comment, because under <c>None</c> the comment is an ordinary one
///         its deletion would orphan. A refusal list is one the next such rule is silently not on.
///         Normalising the tree fixes them all at the one place every tree is made, and changes no
///         token: the formatter has always parsed every file it touches this way.
///     </para>
/// </remarks>
public static class DocumentationComments {
    /// <summary>
    ///     The options a project's sources are analysed under: the build's own, with documentation
    ///     parsed when the build did not ask for it. Everything else — language version, preprocessor
    ///     symbols, features — is the build's, unchanged.
    /// </summary>
    public static CSharpParseOptions ForAnalysis(CSharpParseOptions options) =>
        options.DocumentationMode == DocumentationMode.None
            ? options.WithDocumentationMode(DocumentationMode.Parse)
            : options;

    /// <summary>
    ///     Whether the build asked the compiler for its documentation diagnostics.
    /// </summary>
    public static bool CompilerReportsOn(ParseOptions? options) =>
        options is null || options.DocumentationMode == DocumentationMode.Diagnose;

    /// <summary>
    ///     <c>SK9032</c>, once per run, naming every project whose build did not ask the compiler for
    ///     documentation diagnostics; nothing when there is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ One diagnostic for the run rather than one per project. It is one statement about one cause,
    ///     and Skala's own solution has dozens of projects without the property: a line each is the wall
    ///     of <c>SK9001</c> that the registry's own rationale says gets a tool uninstalled. The projects
    ///     are named — the first <see cref="NamedInTheMessage" /> in the message, every one in the
    ///     detail — so the cardinality is still per project, deduplicated across a multi-targeted
    ///     project's compilations, which are one project with one property.
    /// </remarks>
    public static ImmutableArray<SkalaDiagnostic> Note(ImmutableArray<CompilationUnit> units, string repositoryRoot) {
        var projects = units
            .Where(static unit => unit.DocumentationDiagnosticsOff)
            .Select(static unit => unit.ProjectPath.Length > 0 ? unit.ProjectPath : unit.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (projects.Count == 0) {
            return [];
        }

        var total = units
            .Select(static unit => unit.ProjectPath.Length > 0 ? unit.ProjectPath : unit.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var names = projects
            .Select(project => project.Length > 0 && Path.IsPathRooted(project)
                    ? Path.GetRelativePath(repositoryRoot, project)
                    : project
            )
            .ToList();

        var count = projects.Count.ToString(CultureInfo.InvariantCulture)
            + " of "
            + total.ToString(CultureInfo.InvariantCulture)
            + (total == 1 ? " project does" : " projects do");

        var named = string.Join(", ", names.Take(NamedInTheMessage))
            + (names.Count > NamedInTheMessage
                ? " and " + (names.Count - NamedInTheMessage).ToString(CultureInfo.InvariantCulture) + " more"
                : string.Empty);

        return [
            new SkalaDiagnostic(
                ConfigDiagnosticIds.DocumentationDiagnosticsOff,
                SkalaSeverity.Info,
                $"{count} not set GenerateDocumentationFile, so the compiler's XML-documentation "
                + "diagnostics (CS1570–CS1592, CS1710–CS1739) were not checked there: "
                + named,
                repositoryRoot,
                Detail: string.Join(", ", names)
            )
        ];
    }

    /// <summary>How many projects the message names before it says how many more the detail holds.</summary>
    const int NamedInTheMessage = 3;
}
