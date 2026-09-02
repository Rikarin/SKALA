// A range is only reported where no length can make it valid. `2..2` is the empty slice, `^3..^1`
// runs forwards because the start is `Length - 3` and the end is `Length - 1`, and `0..0` is empty.
class C {
    int[] Empty(int[] values) => values[2..2];

    int[] Forward(int[] values) => values[^3..^1];

    int[] Nothing(int[] values) => values[0..0];
}
