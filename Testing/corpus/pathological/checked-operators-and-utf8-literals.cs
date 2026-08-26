class C {
    static System.ReadOnlySpan<byte> A => "abc"u8;

    public static C operator checked +(C a, C b) => a;

    public static C operator +(C a, C b) => a;
}
