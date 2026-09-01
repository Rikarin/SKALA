// `x.ToString("N2")` and `$"{x:N2}"` agree only where the instance method delegates to
// `IFormattable`, which a user type need not. Not covered.
public sealed class Money {
    public string Line(decimal amount) => $"{amount.ToString("N2")}";
}
