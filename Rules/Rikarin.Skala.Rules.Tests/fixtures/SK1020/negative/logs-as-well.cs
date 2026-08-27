using System;

public sealed class Guarded {
    public static void Use(object source) {
        if (source is null) {
            Console.WriteLine("missing");
            throw new ArgumentNullException(nameof(source));
        }
    }
}
