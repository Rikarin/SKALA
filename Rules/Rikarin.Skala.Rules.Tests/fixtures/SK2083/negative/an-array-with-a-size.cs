using System;

public sealed class Slots {
    public static void Show(int count) {
        var slots = new int[count];
        foreach (var slot in slots) {
            Console.WriteLine(slot);
        }
    }
}
