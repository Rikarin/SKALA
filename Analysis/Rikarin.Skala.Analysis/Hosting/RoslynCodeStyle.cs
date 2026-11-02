using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>Roslyn's own IDE code-style analyzers, shipped beside Skala at the pinned Roslyn version.</summary>
/// <remarks>
///     Naming is deliberately not reimplemented. The three-part <c>dotnet_naming_*</c> language is
///     Roslyn's, including its specificity and word-splitting rules, so Skala loads the supported
///     <c>Microsoft.CodeAnalysis.CSharp.CodeStyle</c> package and selects the analyzer that owns
///     <c>IDE1006</c>.
///     <para>
///         ⚠ Loading the whole package's rule set would silently turn Skala into <c>dotnet format</c>,
///         so the selection is by id and stays narrow. Besides naming it is the ids in
///         <see cref="DefaultSeverities" />, each at a default Skala decides and any project setting
///         overrides (docs/plan/08 § "Roslyn's unused-code and primary-constructor analyzers").
///         ⚠ An analyzer is selected whole, and the unused-value analyzer also carries
///         <c>IDE0058</c> and <c>IDE0060</c>, which are not hosted — <c>IDE0060</c> reports every
///         parameter a delegate- or event-shaped signature is obliged to declare. Those are
///         <see cref="RoslynCodeStyleResult.SilencedIds" />: defaulted to <c>none</c> and dropped by
///         <see cref="AnalyzerHost" /> whatever a project configures, because a project asking for
///         them is asking <c>dotnet format</c>, not Skala.
///     </para>
/// </remarks>
public static class RoslynCodeStyle {
    public const string NamingDiagnosticId = "IDE1006";

    public static string UnusedMemberId => "IDE0051";

    public static string UnreadMemberId => "IDE0052";

    public static string UnusedValueId => "IDE0059";

    public static string PrimaryConstructorId => "IDE0290";

    /// <summary>
    ///     What a hosted id reports at when nothing in the project configures it.
    /// </summary>
    /// <remarks>
    ///     ⚠ These analyzers ship enabled and <c>Hidden</c>, and the gate that makes one report is a
    ///     <c>dotnet_diagnostic.&lt;id&gt;.severity</c> line, not its style option (#73, docs/plan/08).
    ///     <see cref="EditorConfigOptions" /> answers with these only where the project said nothing —
    ///     see <see cref="HostedSeverityDefaults" /> for every setting that wins over them, <c>none</c>
    ///     included. <c>IDE1006</c> is absent on purpose: naming reports nothing until a project writes
    ///     <c>dotnet_naming_*</c> rules, and that is unchanged.
    ///     <para>
    ///         ⚠ <b>Only <c>IDE0290</c> is on by default, and the other three were measured off.</b>
    ///         Skala's bar for a default is zero false positives on the reference trees, and each of
    ///         the three produced one its own options cannot suppress (docs/plan/08 § "Roslyn's
    ///         unused-code and primary-constructor analyzers"): <c>IDE0051</c> on Newtonsoft.Json's
    ///         private constructors that exist so the serializer has no fallback, <c>IDE0052</c> on a
    ///         static field Vixen writes so the JIT cannot elide an allocation, and <c>IDE0059</c> on
    ///         four Vixen locals whose initial value is read while an <c>async</c> local function that
    ///         overwrites them is suspended — Roslyn's data flow runs a local function's writes at its
    ///         call site. They default to <c>none</c>, so the analyzers skip the work, and a project
    ///         turns any of them on with one severity line.
    ///     </para>
    /// </remarks>
    public static FrozenDictionary<string, ReportDiagnostic> DefaultSeverities { get; } =
        new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal) {
            [UnusedMemberId] = ReportDiagnostic.Suppress,
            [UnreadMemberId] = ReportDiagnostic.Suppress,
            [UnusedValueId] = ReportDiagnostic.Suppress,
            [PrimaryConstructorId] = ReportDiagnostic.Info
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     The style option whose <c>:severity</c> suffix configures a hosted id, honoured exactly as
    ///     written.
    /// </summary>
    public static FrozenDictionary<string, string> StyleOptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) {
            [UnusedValueId] = "csharp_style_unused_value_assignment_preference",
            [PrimaryConstructorId] = "csharp_style_prefer_primary_constructors"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Every id Skala reports from this package.</summary>
    public static FrozenSet<string> HostedIds { get; } =
        new[] { NamingDiagnosticId, UnusedMemberId, UnreadMemberId, UnusedValueId, PrimaryConstructorId }
            .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     The hosted ids whose Roslyn fixer <c>skala fix --include</c> turns into a text edit on the one
    ///     file the finding is in (<see cref="HostedCodeFixes" />).
    /// </summary>
    /// <remarks>
    ///     ⚠ None of them is <c>fixIsSafe</c>. They are not in the catalogue, so <c>AnalyzerHost</c>
    ///     already answers unsafe, and each has its own reason: removing a member cannot see
    ///     reflection, serialization or an <c>[UnsafeAccessor]</c>; removing an assignment cannot see a
    ///     read in an inactive <c>#if</c> branch, and the re-bind checks only the active configuration;
    ///     a primary constructor changes the shape a type declares.
    ///     <para>
    ///         ⚠ <c>IDE0052</c> is not here because Roslyn has no fixer for it: in the 5.9.0 payload
    ///         <c>CSharpRemoveUnusedMembersCodeFixProvider</c> declares <c>IDE0051</c> alone, measured
    ///         by loading it. A member that is written and never read is reported and left to a person,
    ///         which is also the right answer for the <c>GC.KeepAlive</c>-by-field shape.
    ///     </para>
    /// </remarks>
    public static FrozenSet<string> EditFixableIds { get; } =
        new[] { UnusedMemberId, UnusedValueId, PrimaryConstructorId }.ToFrozenSet(StringComparer.Ordinal);

    static readonly Lazy<RoslynCodeStyleResult> Loaded = new(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);

    public static RoslynCodeStyleResult Load() => Loaded.Value;

    static RoslynCodeStyleResult LoadCore() {
        var directory = Path.Combine(AppContext.BaseDirectory, "RoslynCodeStyle");
        if (!Directory.Exists(directory)) {
            return Failed(directory, "the Roslyn code-style payload is missing from the Skala installation");
        }

        var loader = new CodeStyleAssemblyLoader(directory);
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        try {
            foreach (var name in new[] {
                         "Microsoft.CodeAnalysis.CodeStyle.dll", "Microsoft.CodeAnalysis.CSharp.CodeStyle.dll"
                     }) {
                var path = Path.Combine(directory, name);
                if (!File.Exists(path)) {
                    return Failed(path, $"'{name}' is missing from the Roslyn code-style payload");
                }

                var reference = new AnalyzerFileReference(path, loader);
                analyzers.AddRange(
                    reference.GetAnalyzers(LanguageNames.CSharp)
                        .Where(static analyzer => analyzer.SupportedDiagnostics.Any(static descriptor =>
                                HostedIds.Contains(descriptor.Id)
                            )
                        )
                );
            }
        } catch (Exception exception) when (exception is IOException
                                                or FileLoadException
                                                or BadImageFormatException
                                                or TypeLoadException
                                                or ReflectionTypeLoadException
                                                or InvalidOperationException) {
            return Failed(directory, $"the Roslyn code-style analyzers could not be loaded: {exception.Message}");
        }

        var distinct = analyzers.DistinctBy(static analyzer => analyzer.GetType().FullName, StringComparer.Ordinal)
            .ToImmutableArray();
        var supported = distinct.SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);
        var missing = HostedIds.Where(id => !supported.Contains(id)).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0) {
            return Failed(
                directory,
                $"the Roslyn code-style payload contains no analyzer for {string.Join(", ", missing)}"
            );
        }

        var silenced = supported.Where(static id => !HostedIds.Contains(id)).ToFrozenSet(StringComparer.Ordinal);
        var categories = distinct.SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Where(static descriptor => HostedIds.Contains(descriptor.Id))
            .DistinctBy(static descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToFrozenDictionary(
                static descriptor => descriptor.Id,
                static descriptor => descriptor.Category,
                StringComparer.Ordinal
            );

        var fixers = new Dictionary<string, CodeFixProvider>(StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var name in new[] {
                     "Microsoft.CodeAnalysis.CodeStyle.Fixes.dll", "Microsoft.CodeAnalysis.CSharp.CodeStyle.Fixes.dll"
                 }) {
            try {
                var fixes = loader.LoadFromPath(Path.Combine(directory, name));
                foreach (var type in fixes.GetTypes()) {
                    if (!IsHostedFixer(type)) {
                        continue;
                    }

                    if (Activator.CreateInstance(type, true) is not CodeFixProvider candidate) {
                        continue;
                    }

                    foreach (var id in candidate.FixableDiagnosticIds.Where(IsFixedHere)) {
                        fixers.TryAdd(id, candidate);
                    }
                }
            } catch (Exception exception) when (exception is IOException
                                                    or FileLoadException
                                                    or BadImageFormatException
                                                    or TypeLoadException
                                                    or ReflectionTypeLoadException
                                                    or MissingMethodException
                                                    or TargetInvocationException) {
                problems.Add($"'{name}' could not be loaded: {exception.Message}");
            }
        }

        var unfixable = EditFixableIds.Append(NamingDiagnosticId)
            .Where(id => !fixers.ContainsKey(id))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unfixable.Length > 0) {
            problems.Add($"the Roslyn code-style payload contains no fixer for {string.Join(", ", unfixable)}");
        }

        // ⚠ A missing fixer costs that id its fix and nothing else. The analyzers still run — a
        // finding without a fix is still a finding — and the fixers that did load still apply.
        return new(
            distinct,
            fixers.ToFrozenDictionary(StringComparer.Ordinal),
            silenced,
            categories,
            [
                ..problems.Select(problem => new SkalaDiagnostic(
                        RuleIds.AnalyzerFailedToLoad,
                        SkalaSeverity.Warning,
                        problem,
                        directory
                    )
                )
            ]
        );
    }

    /// <summary>
    ///     A concrete C# fixer whose type name is one of the hosted ids' fixers.
    /// </summary>
    /// <remarks>
    ///     ⚠ By name before construction: the package carries a fixer per IDE rule, and constructing
    ///     the ones Skala never uses would run their initializers for nothing. Each name was read off
    ///     the 5.9.0 payload; a rename in a later package shows up as the <c>SK9031</c>-family warning
    ///     above, not as a silent loss of a fix.
    /// </remarks>
    static bool IsHostedFixer(Type type) =>
        !type.IsAbstract
        && !type.ContainsGenericParameters
        && typeof(CodeFixProvider).IsAssignableFrom(type)
        && FixerTypeNames.Contains(type.Name)
        && type.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            Type.EmptyTypes
        ) is not null;

    static readonly FrozenSet<string> FixerTypeNames = new[] {
        "NamingStyleCodeFixProvider", "CSharpRemoveUnusedMembersCodeFixProvider",
        "CSharpRemoveUnusedValuesCodeFixProvider", "CSharpUsePrimaryConstructorCodeFixProvider"
    }.ToFrozenSet(StringComparer.Ordinal);

    static bool IsFixedHere(string id) => id == NamingDiagnosticId || EditFixableIds.Contains(id);

    static RoslynCodeStyleResult Failed(string path, string message) =>
        new(
            [],
            FrozenDictionary<string, CodeFixProvider>.Empty,
            FrozenSet<string>.Empty,
            FrozenDictionary<string, string>.Empty,
            [
                new SkalaDiagnostic(
                    RuleIds.AnalyzerFailedToLoad,
                    SkalaSeverity.Warning,
                    message,
                    path
                )
            ]
        );

    /// <summary>
    ///     Shares the host's Roslyn contracts and resolves the code-style implementation assemblies
    ///     beside one another. A private copy of Microsoft.CodeAnalysis would make its
    ///     DiagnosticAnalyzer a different CLR type from the host's.
    /// </summary>
    sealed class CodeStyleAssemblyLoader(string directory) : IAnalyzerAssemblyLoader {
        readonly AssemblyLoadContext context = new CodeStyleLoadContext(directory);

        public void AddDependencyLocation(string fullPath) { }

        public Assembly LoadFromPath(string fullPath) {
            var name = AssemblyName.GetAssemblyName(fullPath);
            return FindLoaded(name) ?? context.LoadFromAssemblyPath(Path.GetFullPath(fullPath));
        }
    }

    sealed class CodeStyleLoadContext(string directory) : AssemblyLoadContext("skala/roslyn-code-style") {
        protected override Assembly? Load(AssemblyName assemblyName) {
            if (FindLoaded(assemblyName) is { } loaded) {
                return loaded;
            }

            var beside = Path.Combine(directory, assemblyName.Name + ".dll");
            return File.Exists(beside) ? LoadFromAssemblyPath(beside) : null;
        }
    }

    static Assembly? FindLoaded(AssemblyName requested) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), requested));
}

/// <summary>What <see cref="RoslynCodeStyle.Load" /> found.</summary>
/// <param name="Analyzers">The analyzers owning a hosted id, each selected whole.</param>
/// <param name="Fixers">Roslyn's fixer for each hosted id that has one that loaded.</param>
/// <param name="SilencedIds">Ids the selected analyzers carry that Skala never reports.</param>
/// <param name="Categories">Each hosted id's descriptor category, for the bulk severity keys.</param>
/// <param name="Diagnostics">Why something could not be loaded.</param>
public sealed record RoslynCodeStyleResult(
    ImmutableArray<DiagnosticAnalyzer> Analyzers,
    FrozenDictionary<string, CodeFixProvider> Fixers,
    FrozenSet<string> SilencedIds,
    FrozenDictionary<string, string> Categories,
    ImmutableArray<SkalaDiagnostic> Diagnostics) {
    /// <summary>Roslyn's naming fixer, or null when it could not be loaded.</summary>
    public CodeFixProvider? NamingFixer => Fixers.GetValueOrDefault(RoslynCodeStyle.NamingDiagnosticId);
}
