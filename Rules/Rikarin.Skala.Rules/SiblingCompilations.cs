using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules;

/// <summary>
///     The other target frameworks' compilations of the project this analyzer is running over.
/// </summary>
/// <remarks>
///     ⚠ <b>An analyzer cannot reach a sibling compilation on its own</b>, and that is the whole
///     reason this exists. Roslyn hands a <see cref="DiagnosticAnalyzer" /> exactly one
///     <see cref="Compilation" />; <c>MSBuildWorkspace</c> and a binlog both open a multi-targeted
///     project as one compilation per moniker, so a rule asking
///     <c>Compilation.GetTypeByMetadataName</c> whether a framework type exists is answered by
///     whichever moniker it happens to be running in — while the fix it offers lands in a source file
///     <em>every</em> moniker compiles.
///     <para>
///         The loader knows the whole set (see <c>MultiTargetLink</c>), so it publishes it on the
///         <see cref="AnalyzerConfigOptionsProvider" /> it already builds for the driver, which is the
///         one object on <see cref="AnalyzerOptions" /> a host may substitute freely. A rule reads it
///         through <see cref="For" /> and gets an empty array under any host that does not — a
///         single-target project, a loose load, <c>csc</c>, or Rider — where the question does not
///         arise.
///     </para>
/// </remarks>
public interface ISiblingCompilations {
    /// <summary>The same project's other target frameworks. Empty when there are none.</summary>
    ImmutableArray<Compilation> Siblings { get; }
}

/// <summary>
///     Asking a framework-availability question of every compilation a document belongs to (#343).
/// </summary>
/// <remarks>
///     ⚠ <b>The reason a rule may not settle this on its loaded compilation alone.</b> <c>SK1023</c>
///     rewrote <c>readonly object gate = new();</c> to <c>System.Threading.Lock</c> on a
///     <c>netstandard2.1;net10.0</c> library: the type exists in <c>net10.0</c>, the analysis ran
///     there, and the <c>netstandard2.1</c> leg — the half that goes into Unity/IL2CPP — stopped
///     compiling with <c>CS0234</c> on a file <c>skala fix --safe</c> had just written.
///     <para>
///         ⚠ The predicate is the rule's <em>whole</em> availability condition, not just "the type
///         resolves". <c>SK1023</c>'s includes the shape checks that tell the real
///         <c>System.Threading.Lock</c> apart from a same-named type declared in source, and a
///         sibling that has a different one of those is just as unable to compile the rewrite.
///     </para>
/// </remarks>
public static class FrameworkAvailability {
    /// <summary>The sibling compilations the host published, or none.</summary>
    public static ImmutableArray<Compilation> For(AnalyzerOptions options) =>
        options.AnalyzerConfigOptionsProvider is ISiblingCompilations provider
            ? provider.Siblings
            : ImmutableArray<Compilation>.Empty;

    /// <summary>
    ///     The source paths that at least one sibling compiles and that <paramref name="supported" />
    ///     rejects — the documents a framework-dependent rewrite must not be offered on.
    /// </summary>
    /// <remarks>
    ///     ⚠ Keyed on the path rather than assumed project-wide. A moniker may compile a different
    ///     set of files (a <c>Compile</c> item under a <c>Condition</c>), so "this project has a
    ///     framework that lacks the type" is not the same claim as "this file is compiled by one".
    ///     Computed once per compilation start; the loop is over the sibling's trees, which is the
    ///     only place the membership is recorded.
    /// </remarks>
    public static ImmutableHashSet<string> PathsWithout(AnalyzerOptions options, Func<Compilation, bool> supported) {
        var siblings = For(options);
        if (siblings.IsEmpty) {
            return ImmutableHashSet<string>.Empty;
        }

        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var sibling in siblings) {
            if (supported(sibling)) {
                continue;
            }

            foreach (var tree in sibling.SyntaxTrees) {
                if (tree.FilePath is { Length: > 0 } path) {
                    builder.Add(path);
                }
            }
        }

        return builder.ToImmutable();
    }
}
