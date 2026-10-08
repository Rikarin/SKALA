using Microsoft.CodeAnalysis.Diagnostics;
using System;

namespace Rikarin.Skala.Rules;

/// <summary>
///     The compiler that builds the compilation an analyzer is running over, when the host knows it (#517).
/// </summary>
/// <remarks>
///     ⚠ <b>Only a binlog knows.</b> The recorded <c>Csc</c> line starts with the compiler's path, which
///     names the SDK's (<c>…/sdk/&lt;version&gt;/Roslyn/bincore/csc.exe</c>) or a
///     <c>Microsoft.Net.Compilers.Toolset</c> package's
///     (<c>…/microsoft.net.compilers.toolset/&lt;version&gt;/tasks/…/csc.exe</c>). A workspace load,
///     a loose load, <c>csc</c> and Rider publish nothing, and a rule reading this gets an empty
///     string: "unknown", which is not "the SDK's".
///     <para>
///         Published the way <see cref="ISiblingCompilations" /> is, on the
///         <see cref="AnalyzerConfigOptionsProvider" /> the host builds for the driver, because that is the
///         one object on <see cref="AnalyzerOptions" /> a host may substitute. ⚠ The value is in the
///         diagnostic-cache key: a finding computed under one compiler must not be served under another.
///     </para>
/// </remarks>
public interface ICompilerIdentity {
    /// <summary>The compiler's path as the build recorded it, or empty.</summary>
    string CompilerPath { get; }
}

/// <summary>Reading <see cref="ICompilerIdentity" />.</summary>
public static class CompilerIdentity {
    const string Toolset = "microsoft.net.compilers.toolset";

    /// <summary>The compiler path the host published, or empty.</summary>
    public static string PathOf(AnalyzerOptions options) =>
        options.AnalyzerConfigOptionsProvider is ICompilerIdentity identity ? identity.CompilerPath : string.Empty;

    /// <summary>
    ///     Whether the published compiler is a <c>Microsoft.Net.Compilers.Toolset</c> package older than
    ///     <paramref name="floor" /> — or one whose version cannot be read, which proves nothing either.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>false</c> for an empty path and for the SDK's own compiler: a rule that needs a positive
    ///     proof of the compiler must not take "no toolset was seen" as one under a host that cannot see.
    ///     The package directory is matched as a whole path segment, so <c>…toolset.framework</c> (the
    ///     .NET Framework flavour of the same package) counts and a lookalike name does not.
    /// </remarks>
    public static bool IsToolsetOlderThan(AnalyzerOptions options, Version floor) =>
        IsToolsetOlderThan(PathOf(options), floor);

    /// <inheritdoc cref="IsToolsetOlderThan(AnalyzerOptions, Version)" />
    public static bool IsToolsetOlderThan(string compilerPath, Version floor) {
        var segments = compilerPath.Split('/', '\\');
        for (var i = 0; i < segments.Length - 1; i++) {
            if (!string.Equals(segments[i], Toolset, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(segments[i], Toolset + ".framework", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var version = segments[i + 1];
            var prerelease = version.IndexOf('-');
            if (prerelease >= 0) {
                version = version.Substring(0, prerelease);
            }

            return !Version.TryParse(version, out var parsed) || parsed < floor;
        }

        return false;
    }
}
