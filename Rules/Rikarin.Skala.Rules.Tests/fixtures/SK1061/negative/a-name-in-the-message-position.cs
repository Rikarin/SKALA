using System;

// ⚠ Added because a sabotage that widened the position test to "any parameter" was caught only by
// the batch theory and by no fixture at all. `ArgumentException(string message)` takes prose, and
// the prose people write there is very often a parameter name — which is exactly why reading the
// *value* rather than the parameter would be wrong.
public sealed class Messages {
    public void Take(int count) {
        if (count < 0) {
            throw new ArgumentException("count");
        }
    }
}
