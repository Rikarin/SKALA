// C# 14's instance compound-assignment operator is declared `void`, the one operator that is.
public sealed class Counter {
    /// <summary>The running total.</summary>
    public int Total { get; private set; }

    /// <summary>Adds to the total.</summary>
    /// <param name="amount">How much to add.</param>
    /// <returns>The new total.</returns>
    public void operator +=(int amount) => Total += amount;
}
