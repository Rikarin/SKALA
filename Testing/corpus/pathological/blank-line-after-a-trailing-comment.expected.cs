// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-28
namespace Fuzz.N1 {
    internal class T2 {
        // fuzz
        public override Span<Guid> M23(
            Dictionary<int, List<int>> p24,
            (byte First, object Second) p25,
            decimal p26,
            byte p27 = default
        ) { } // fuzz

        public static readonly (Nullable<bool> First, IReadOnlyDictionary<long, long> Second)
            F65 = value is null; // fuzz
    } // fuzz
}
