// `text is int` is CS8121: no conversion reaches `int` from `string`, so there is no pattern.
class Unrelated {
    public bool Test(string text) => typeof(int).IsInstanceOfType(text);
}
