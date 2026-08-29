// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class C {
    static System.ReadOnlySpan<byte> A => "abc"u8;

    public static C operator checked +(C a, C b) => a;

    public static C operator +(C a, C b) => a;
}
