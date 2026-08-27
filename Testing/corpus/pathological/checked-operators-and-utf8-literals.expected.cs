// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-26
class C {
    static System.ReadOnlySpan<byte> A => "abc"u8;

    public static C operator checked +(C a, C b) => a;

    public static C operator +(C a, C b) => a;
}
