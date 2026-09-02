// ⚠ The crossing fixture. A non-nullable return in a nullable-enabled context is CS8603's
// ground, measured on a probe against the .NET 10.0.400 SDK, and ADR-008 says the platform's own
// diagnostic is used rather than duplicated. Reporting here would put two findings on one line.
namespace Fixtures {
    sealed class Invoice {
        public override string ToString() {
            return null;
        }
    }
}
