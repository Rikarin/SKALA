// The shape the positive fixtures are a corruption of, written correctly.
sealed class Contact {
    string firstName = "";
    string lastName = "";

    public string FirstName {
        get => firstName;
        set => firstName = value;
    }

    public string LastName {
        get => lastName;
        set => lastName = value;
    }
}
