public static class Anonymous {
    // Under `var` the names come from the literal, and deleting them deletes them from the type.
    public static object Pair(string name, int age) {
        var person = (Name: name, Age: age);
        return person;
    }
}
