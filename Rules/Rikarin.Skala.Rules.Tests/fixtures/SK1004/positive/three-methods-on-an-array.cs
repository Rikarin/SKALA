namespace Fixtures {
    static class ArrayHelpers {
        public static bool IsEmpty(this int[] items) => items.Length == 0;

        public static int At(this int[] items, int index) => items[index];

        public static int Total(this int[] items) {
            var sum = 0;
            foreach (var item in items) {
                sum += item;
            }

            return sum;
        }
    }
}
