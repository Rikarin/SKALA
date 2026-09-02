// One explicit value declines the whole enum. `Second` and `Third` are numbered by the compiler,
// but `First = 1` is somebody choosing a base, and the declaration cannot say what for.
enum Step {
    First = 1,
    Second,
    Third
}

sealed class Runner {
    public Step Combine(Step left, Step right) => left | right;

    public Step Invert(Step step) => ~step;
}
