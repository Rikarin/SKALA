using System;

public static class LambdaReturn {
    // The lambda's return goes to the delegate it is converted to, not to `Go`'s written `int?`.
    // Nothing at this return writes a type down, so the walk stops at the lambda rather than
    // climbing into the enclosing method and reading a promise that belongs to another member.
    public static int? Go(int value) {
        Func<int?> inner = () => {
            return new int?(value);
        };

        return inner();
    }
}
