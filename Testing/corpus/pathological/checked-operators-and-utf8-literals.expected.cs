// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class C {
    static System.ReadOnlySpan<byte> A => "abc"u8;

    public static C operator checked +(C a, C b) => a;

    public static C operator +(C a, C b) => a;
}
