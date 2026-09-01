namespace Contoso.Design;

// Nothing has been claimed, so nothing is unkept. Both of these are what the rule's advice points at.
public static class Endpoints {
    public static string Health => "/health";
}

public sealed class Report {
    public string Title { get; init; } = string.Empty;
}
