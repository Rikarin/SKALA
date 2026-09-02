// ⚠ A `?:` condition need not be a `bool`. Replacing the conditional with `flag` would change the
// expression's type from `bool` to `Flag`.
struct Flag {
    public static bool operator true(Flag value) => true;

    public static bool operator false(Flag value) => false;
}

class C {
    public static bool Run(Flag flag) => flag ? true : false;
}
