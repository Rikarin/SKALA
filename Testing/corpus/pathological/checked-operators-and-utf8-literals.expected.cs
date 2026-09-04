// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class C {
    static System.ReadOnlySpan<byte> A => "abc"u8;

    public static C operator checked +(C a, C b) => a;

    public static C operator +(C a, C b) => a;
}
