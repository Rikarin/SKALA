// Effective accessibility, not the declared modifier: `public` on an `internal` class is internal,
// so deleting the overload cannot break anybody outside the assembly.
namespace Fixtures {
    internal sealed class Buffer {
        public string Take(int count) => Take(count, ' ');

        public string Take(int count, char pad) => new string(pad, count);

        internal string Use() => Take(3);
    }
}
