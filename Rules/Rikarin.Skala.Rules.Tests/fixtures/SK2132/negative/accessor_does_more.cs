using System;

// ⚠ The moment an accessor does something in addition to reaching for storage, what it reaches for
// is a decision rather than a name that was mistyped. Declined by the shape test, not by a list.
sealed class Contact {
    string firstName = "";
    string lastName = "";

    public string FirstName {
        get => firstName;
        set => firstName = value;
    }

    public string LastName {
        get {
            Console.WriteLine("reading the display name");
            return firstName;
        }

        set => lastName = value;
    }
}
