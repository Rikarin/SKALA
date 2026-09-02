// The second property was written by copying the first, and the getter's field name was not
// changed with it. `LastName` returns the first name.
sealed class Contact {
    string firstName = "";
    string lastName = "";

    public string FirstName {
        get => firstName;
        set => firstName = value;
    }

    public string LastName {
        get => firstName;
        set => lastName = value;
    }
}
