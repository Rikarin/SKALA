public sealed class HTMLClass {
    public string Tag { get; init; } = string.Empty;
}

public readonly struct RGBStruct {
    public RGBStruct(int packed) {
        Packed = packed;
    }

    public int Packed { get; }
}
