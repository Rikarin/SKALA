public sealed class MasterClass {
    public string Topic { get; init; } = string.Empty;
}

public sealed class CharacterClass {
    public string Pattern { get; init; } = string.Empty;
}

public sealed class EquivalenceClass {
    public int Representative { get; init; }
}

public sealed class WeightClass {
    public int Upper { get; init; }
}
