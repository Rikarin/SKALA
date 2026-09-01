// ⚠ The rule exists because precedence and reader expectation disagree about `?:`. Inside an
// interpolation hole they cannot: the hole has explicit delimiters, and C# *requires* a conditional
// written in one to be parenthesised, because a bare `:` there is the format specifier. There is
// nothing left to mis-group, so the hole restarts the count exactly as a lambda body does.
namespace Fixtures;

class Report {
    public static string Describe(string name, bool? result) =>
        result is null
            ? $"There is no switch called '{name}'."
            : $"{name} is {(result.Value ? "on" : "off")}.";
}
