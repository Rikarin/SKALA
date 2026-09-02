using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>One analyzer package the user asked to host (ADR-008).</summary>
public sealed record HostedPackage(string Package, string Version);

/// <summary>The analyzers a package produced, or the reason it produced none.</summary>
public sealed record HostedResult(
    ImmutableArray<DiagnosticAnalyzer> Analyzers,
    ImmutableArray<ToolExtension> Extensions,
    ImmutableArray<SkalaDiagnostic> Diagnostics);

/// <summary>
///     Loading third-party analyzers on request, and never bundling any (ADR-008).
/// </summary>
/// <remarks>
///     Bundling <c>SonarAnalyzer.CSharp</c>, Roslynator or Meziantou would make Skala's findings the
///     union of four projects' opinions and Skala's false-positive budget the sum of four projects'
///     false positives — and in Sonar's case would put an LGPL obligation on an Apache-2.0 tool.
///     Hosting them is the answer, and the corollary is that Skala must be worth using with nothing
///     hosted.
///     <para>
///         ⚠ One <see cref="AssemblyLoadContext" /> per package, and it matters: half of these bundle their
///         own <c>Newtonsoft.Json</c> or <c>System.Collections.Immutable</c>, and two analyzers wanting
///         different versions of one helper in a single context is a <c>TypeLoadException</c> whose message
///         names neither analyzer.
///     </para>
///     <para>
///         ⚠ Failure to load is <c>SK9031</c> and is <b>never fatal</b>. A package that is missing, is for
///         another framework, or drags a conflicting dependency is a configuration problem to report, not a
///         reason to produce no report at all.
///     </para>
/// </remarks>
public static class HostedAnalyzers {
    /// <summary>Where restored packages live. Tool-local, never the user's global packages folder.</summary>
    public static string PackageRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".skala",
        "packages"
    );

    /// <summary>
    ///     Whether <c>skala.jsonc</c> asks for <c>resharper_*_highlighting</c> to set rule severities.
    /// </summary>
    /// <remarks>
    ///     ⚠ Default false. docs/plan/03 § "Severities" and docs/plan/16 § Q5: the values in an export
    ///     were chosen for ReSharper's inspections, and the author's own export sets
    ///     <c>resharper_use_throw_if_null_method_highlighting = none</c>.
    /// </remarks>
    public static bool ReadsReSharperSeverities(string? toolConfigPath) {
        if (toolConfigPath is null || !File.Exists(toolConfigPath)) {
            return false;
        }

        try {
            using var document = JsonDocument.Parse(
                File.ReadAllText(toolConfigPath),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            );

            return document.RootElement.TryGetProperty("analysis", out var analysis)
                && analysis.TryGetProperty("resharperSeverities", out var value)
                && value.ValueKind == JsonValueKind.True;
        } catch (JsonException) {
            return false;
        } catch (IOException) {
            return false;
        }
    }

    /// <summary>Reads <c>analysis.hostedAnalyzers</c> out of <c>skala.jsonc</c>.</summary>
    public static ImmutableArray<HostedPackage> Read(string? toolConfigPath) {
        if (toolConfigPath is null || !File.Exists(toolConfigPath)) {
            return [];
        }

        try {
            using var document = JsonDocument.Parse(
                File.ReadAllText(toolConfigPath),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
            );

            if (!document.RootElement.TryGetProperty("analysis", out var analysis)
                || !analysis.TryGetProperty("hostedAnalyzers", out var hosted)
                || hosted.ValueKind != JsonValueKind.Array) {
                return [];
            }

            var builder = ImmutableArray.CreateBuilder<HostedPackage>();
            foreach (var entry in hosted.EnumerateArray()) {
                if (entry.TryGetProperty("package", out var package) && package.GetString() is { Length: > 0 } name) {
                    builder.Add(
                        new HostedPackage(
                            name,
                            entry.TryGetProperty("version", out var version) ? version.GetString() ?? "*" : "*"
                        )
                    );
                }
            }

            return builder.ToImmutable();
        } catch (JsonException) {
            return [];
        } catch (IOException) {
            return [];
        }
    }

    /// <summary>
    ///     Loads every requested package from the tool-local folder.
    /// </summary>
    /// <remarks>
    ///     ⚠ It does not restore. <c>dotnet restore</c> from inside an analysis run is a network call in
    ///     the middle of a pre-commit hook, so the restore is a separate, explicit step
    ///     (<c>skala analyzers restore</c>) and this reports <c>SK9031</c> when the folder is not there.
    ///     A tool that silently reaches the network during a build is a tool that fails on an aeroplane.
    /// </remarks>
    public static HostedResult Load(ImmutableArray<HostedPackage> packages) {
        if (packages.IsEmpty) {
            return new HostedResult([], [], []);
        }

        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        var extensions = ImmutableArray.CreateBuilder<ToolExtension>();
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        foreach (var package in packages) {
            var directory = Locate(package);
            if (directory is null) {
                diagnostics.Add(
                    new SkalaDiagnostic(
                        RuleIds.AnalyzerFailedToLoad,
                        SkalaSeverity.Warning,
                        $"'{package.Package}' {package.Version} is not in {PackageRoot}; run `skala analyzers restore`",
                        PackageRoot
                    )
                );

                continue;
            }

            var context = new PackageLoadContext(package.Package, directory);
            var loaded = 0;
            foreach (var assembly in Directory.EnumerateFiles(directory, "*.dll")
                         .OrderBy(
                             static file => file,
                             StringComparer.Ordinal
                         )) {
                try {
                    foreach (var analyzer in Instantiate(context.LoadFromAssemblyPath(assembly))) {
                        analyzers.Add(analyzer);
                        loaded++;
                    }
                } catch (Exception exception) when (exception is BadImageFormatException
                                                        or FileLoadException
                                                        or ReflectionTypeLoadException
                                                        or TypeLoadException
                                                        or MissingMethodException
                                                        or IOException) {
                    // ⚠ Never fatal. See the type's remarks.
                    diagnostics.Add(
                        new SkalaDiagnostic(
                            RuleIds.AnalyzerFailedToLoad,
                            SkalaSeverity.Warning,
                            $"'{Path.GetFileName(assembly)}' from '{package.Package}' did not load: {exception.Message}",
                            assembly
                        )
                    );
                }
            }

            extensions.Add(new ToolExtension(package.Package, package.Version, loaded));
        }

        return new(analyzers.ToImmutable(), extensions.ToImmutable(), diagnostics.ToImmutable());
    }

    static string? Locate(HostedPackage package) {
        var root = Path.Combine(PackageRoot, package.Package.ToLowerInvariant());
        if (!Directory.Exists(root)) {
            return null;
        }

        var versions = Directory.GetDirectories(root)
            .OrderByDescending(
                static directory => directory,
                StringComparer.Ordinal
            );

        foreach (var version in versions) {
            var analyzers = Path.Combine(version, "analyzers", "dotnet", "cs");
            if (Directory.Exists(analyzers)) {
                return analyzers;
            }
        }

        return null;
    }

    static IEnumerable<DiagnosticAnalyzer> Instantiate(Assembly assembly) {
        Type[] types;
        try {
            types = assembly.GetTypes();
        } catch (ReflectionTypeLoadException exception) {
            types = [.. exception.Types.Where(static type => type is not null)!];
        }

        foreach (var type in types) {
            if (type is null
                || type.IsAbstract
                || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                || type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is null) {
                continue;
            }

            DiagnosticAnalyzer? instance = null;
            try {
                instance = Activator.CreateInstance(type) as DiagnosticAnalyzer;
            } catch (MissingMethodException) {
                // A [DiagnosticAnalyzer] with no accessible parameterless constructor is not one
                // Roslyn's own loader could have built either. Skipping it is what the compiler does.
            } catch (TargetInvocationException) {
                // The analyzer's own constructor threw. Hosting the rest of the package is worth
                // more than losing every rule in it to one type that cannot be constructed; a rule
                // that never loads reports nothing, which the negative fixtures would not notice.
            }

            if (instance is not null) {
                yield return instance;
            }
        }
    }

    /// <summary>
    ///     ⚠ One per package, so that two analyzers depending on different versions of one helper
    ///     library do not collide — which they will.
    /// </summary>
    /// <remarks>
    ///     Roslyn's own types are deliberately <em>not</em> isolated: an analyzer loaded into a private
    ///     context with its own copy of <c>Microsoft.CodeAnalysis</c> implements a
    ///     <c>DiagnosticAnalyzer</c> that is not the one the host knows, and the cast fails. The
    ///     resolver therefore returns null for anything the default context already has, which lets the
    ///     shared types unify and isolates everything else.
    /// </remarks>
    sealed class PackageLoadContext(string name, string directory) : AssemblyLoadContext("skala/" + name) {
        readonly AssemblyDependencyResolver resolver = new(directory + Path.DirectorySeparatorChar);

        protected override Assembly? Load(AssemblyName assemblyName) {
            if (assemblyName.Name is { } simple
                && (simple.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
                    || simple.StartsWith("System.", StringComparison.Ordinal)
                    || simple == "netstandard"
                    || simple == "mscorlib")) {
                return null;
            }

            var beside = Path.Combine(directory, assemblyName.Name + ".dll");
            if (File.Exists(beside)) {
                return LoadFromAssemblyPath(beside);
            }

            var resolved = resolver.ResolveAssemblyToPath(assemblyName);
            return resolved is null ? null : LoadFromAssemblyPath(resolved);
        }
    }
}
