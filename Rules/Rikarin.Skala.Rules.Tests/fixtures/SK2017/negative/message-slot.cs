using System;

public sealed class Overloading {
    // ⚠ The same slot is a message on these two constructors — `ArgumentNullException(message,
    // innerException)` and `ArgumentException(message, innerException)`. Counting arguments instead
    // of reading the constructor symbol would report the message text of every one of them.
    public void Wrap(string value, Exception inner) {
        if (value is null) {
            throw new ArgumentNullException("vlaue was null", inner);
        }

        if (value.Length == 0) {
            throw new ArgumentException("vlaue was empty", inner);
        }

        throw new ArgumentException("vlaue was rejected");
    }
}
