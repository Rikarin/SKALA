// The constant must be the literal `0`. A comparison against anything else is a question about
// where the match is, which the rule has no opinion about.
class C {
    bool Deep(string path) => path.IndexOf(':') > 2;

    bool Bounded(string path, int limit) => path.IndexOf(':') > limit;
}
