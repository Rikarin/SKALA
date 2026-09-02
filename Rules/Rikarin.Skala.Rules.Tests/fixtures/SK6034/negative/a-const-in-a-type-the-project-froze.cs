// analyzer-option: dotnet_code_quality.SK6034.frozen_constant_types = ProtocolVersions, Limits
// ⚠ #330: the exemption is declared by the project, never recognised by the analyzer. The key
// defaults to empty, so nothing is exempt until a project names the containing type — which is the
// "on purpose rather than by default" the rule's own rationale asks for.

public static class ProtocolVersions {
    public const string Wire = "v3";
}
