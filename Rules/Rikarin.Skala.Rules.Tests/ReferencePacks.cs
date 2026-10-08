using Microsoft.CodeAnalysis;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The reference assemblies a real <c>net10.0</c> or <c>net9.0</c> build compiles against (#515).
/// </summary>
/// <remarks>
///     ⚠ <b>Throws rather than falling back</b> when a pack is missing. A fixture that asked for
///     <c>net9.0</c> and silently got the test host's references would measure the host — and for the
///     one rule this exists for, the host's <c>System.Private.CoreLib</c> declines everything, so a
///     negative would pass for the wrong reason and nobody would see it.
/// </remarks>
public static class ReferencePacks {
    static readonly ConcurrentDictionary<string, ImmutableArray<MetadataReference>> Loaded =
        new(StringComparer.Ordinal);

    /// <summary>The monikers a fixture may name, validated when the fixture is read.</summary>
    public static string Known(string framework) =>
        DirectoryOf(framework) is not null
            ? framework
            : throw new InvalidOperationException(
                $"'{framework}' is not a fixture reference pack; the packs are net10.0 and net9.0."
            );

    /// <summary>Every reference assembly in the pack.</summary>
    public static ImmutableArray<MetadataReference> For(string framework) =>
        Loaded.GetOrAdd(framework, static framework => Load(framework));

    static ImmutableArray<MetadataReference> Load(string framework) {
        var directory = DirectoryOf(framework)!;
        if (!Directory.Exists(directory)) {
            throw new InvalidOperationException(
                $"the {framework} reference pack is not at {directory}; restore the test project."
            );
        }

        return [
            ..Directory.GetFiles(directory, "*.dll")
                .Order(StringComparer.Ordinal)
                .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ];
    }

    static string? DirectoryOf(string framework) =>
        typeof(ReferencePacks).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "SkalaReferencePack:" + framework)
            ?.Value;
}
