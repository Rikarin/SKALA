using System;

public sealed class Work {
    // `DateTime.Now` additionally jumps an hour twice a year, so this can be negative by 3 600 000 ms.
    public double Milliseconds() {
        DateTime start = DateTime.Now;
        Console.WriteLine("working");
        return (DateTime.Now - start).TotalMilliseconds;
    }
}
