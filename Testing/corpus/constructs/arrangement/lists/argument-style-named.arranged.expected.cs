// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// The two `resharper_arguments_*` keys beside the four in argument-style.cs.
//
// ⚠ `arguments_named` is a fifth bucket and not a refinement of `arguments_other`: it takes the
// arguments that *refer to* something by name — a simple name or a member access — and leaves the
// rest where they were. Measured both ways round against the oracle, with each key at `named` on its
// own; the two sets partition the non-literal, non-lambda arguments with nothing in both.
//
// ⚠ `arguments_skip_single` gates the whole call rather than one argument, which is what makes it
// observable: at `true` a one-argument call keeps a name that a two-argument call loses.
public class ArgumentStyleNamed {
    public int Field;

    public int Property { get; set; }

    public void Take(int number, int other, string text, Action callback) { }

    public void One(int number) { }

    public void Two(int number, int other) { }

    // Every argument here is a named expression except the string and the lambda, which
    // arguments_string_literal and arguments_anonymous_function own. arguments_named turns the first
    // two into `number: Field, other: Property`; arguments_other leaves them alone.
    public void Names() {
        Take(Field, Property, "x", Handler);
    }

    // ⚠ A member access is a named expression too, and an invocation is not: with
    // arguments_named = named the oracle names `holder.Field` and leaves `Compute()` positional.
    public void Members(ArgumentStyleNamed holder) {
        Take(holder.Field, Compute(), "x", () => { });
    }

    // Redundant names on calls of one and of two arguments. The export strips both; with
    // arguments_skip_single = true the one-argument call keeps its name and the other still loses
    // its own.
    public void Single() {
        One(Field);
        Two(Field, Property);
    }

    void Handler() { }

    static int Compute() => 1;
}
