using System;
using System.Text.RegularExpressions;

// ⚠ An instance `IsMatch` inherits the timeout its constructor was given, so the construction site is
// the only place the fact lives. Reporting the call as well would report a correctly-built regex once
// for every use of it.
public static class Validator {
    static readonly Regex Bounded = new(@"^(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(100));

    public static bool First(string input) => Bounded.IsMatch(input);

    public static bool Second(string input) => Bounded.Match(input).Success;
}
