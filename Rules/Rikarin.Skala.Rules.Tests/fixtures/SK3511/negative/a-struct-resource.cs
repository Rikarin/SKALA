using System;

public struct Rental : IDisposable {
    public int Length { get; set; }

    public void Dispose() { }
}

public sealed class Consumer {
    // ⚠ A `using` local is read-only, so `rental.Length = 4` after the declaration is CS1654. The
    // hoisted form does not compile and there is no other one.
    public void Take() {
        using var rental = new Rental { Length = 4 };
        Console.WriteLine(rental.Length);
    }
}
