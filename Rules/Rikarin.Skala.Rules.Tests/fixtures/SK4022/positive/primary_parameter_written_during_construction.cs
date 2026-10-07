// The compiler's own two exceptions: a write in a variable initializer and one in an `init` accessor
// are construction, and `readonly struct Seeded` compiles with both (#408). Before #408 this rule
// declined here; it shares PrimaryConstructorWrites with SK2194 now.
struct Seeded(int seed) {
    public int First { get; } = seed++;

    public int Seed {
        get => seed;
        init => seed = value;
    }
}
