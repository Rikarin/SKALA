// ⚠ `Max(y, x)` and `Add(b, a)` are the vocabulary of code that reverses on purpose, and a
// one-letter name says nothing about intent either way. Below three characters the rule declines,
// which is the difference between a finding and a coin toss.
namespace Fixtures {
    sealed class Arithmetic {
        public int Run(int x, int y) => Max(y, x) + Add(y, x);

        static int Max(int x, int y) => x > y ? x : y;

        static int Add(int x, int y) => x + y;
    }
}
