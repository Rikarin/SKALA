// `in` passes a read-only reference; `readonly struct` accepts it, and so does passing to a
// `ref readonly` parameter with `in`.
namespace Fixtures {
    struct Measure(int width) {
        public int Doubled() => Twice(in width) + Peek(in width);

        static int Twice(in int value) => value * 2;

        static int Peek(ref readonly int value) => value;
    }
}
