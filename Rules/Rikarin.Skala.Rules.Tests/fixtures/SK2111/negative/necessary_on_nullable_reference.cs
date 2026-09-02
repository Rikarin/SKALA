// The suppression is load-bearing: without it this is CS8602.
namespace Fixtures {
    sealed class Trimmer {
        public int Measure(string? text) => text!.Length;
    }
}
