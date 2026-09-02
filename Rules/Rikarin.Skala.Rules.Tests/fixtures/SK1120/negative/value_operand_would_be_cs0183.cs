// `count is object` is CS0183 -- the answer is already known -- so the fix would add a warning.
class ValueOperand {
    public bool Test(int count) => typeof(object).IsInstanceOfType(count);
}
