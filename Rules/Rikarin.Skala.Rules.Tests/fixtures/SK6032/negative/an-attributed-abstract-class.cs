using System;

namespace Contoso.Design;

// A framework reads the attribute, so the declaration's shape is not the whole story.
[Obsolete("Use Pipeline instead.")]
public abstract class LegacyPipeline {
    public string Name { get; init; } = string.Empty;
}
