public sealed class Registry {
    // The other half of the guard the fixture next door used to be covering by accident: a bound that
    // is an expression rather than the receiver's own count is a loop over part of the collection, and
    // `foreach` visits all of it.
    public static int AllButTheLast(int[] numbers) {
        var total = 0;
        for (var i = 0; i < numbers.Length - 1; i++) {
            total += numbers[i];
        }

        return total;
    }
}
