// Anti-vacuity for the positive set: a nullable-oblivious file is not itself a finding.
#nullable disable
namespace Fixtures {
    sealed class Plain {
        public int Measure(string text) => text.Length;
    }
}
