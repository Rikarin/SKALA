// `++` before the operand and `??=` are writes like their siblings (CS9114 under `readonly struct`).
namespace Fixtures {
    sealed class Labels(int count, string? title) {
        public int Next() => ++count;

        public string Title() => title ??= "untitled";
    }
}
