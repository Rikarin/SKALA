public sealed class Table {
    public string Row(decimal amount) => string.Format("{0,10:N2}", amount);
}
