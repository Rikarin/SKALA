// The context is asked at the suppression's own position, not at the top of the file.
#nullable disable
namespace Fixtures {
    sealed class Mixed {
#nullable enable
        public int Measure(string? text) => text!.Length;
#nullable restore
    }
}
