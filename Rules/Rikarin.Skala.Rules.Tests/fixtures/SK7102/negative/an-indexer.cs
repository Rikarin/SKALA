public sealed class Row {
    /// <summary>Reads a cell.</summary>
    /// <param name="index">The column.</param>
    /// <returns>The cell's text.</returns>
    public string this[int index] => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
