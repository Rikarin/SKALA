using System;

// ⚠ The worse form, and the one the rationale is about: erasure takes the arguments with it, so
// `Register()` never runs and nothing anywhere says so.
partial class Importer {
    partial void OnRowRead(string row);

    public void Read() {
        OnRowRead(Register());
    }

    string Register() {
        Console.WriteLine("registered");
        return "row";
    }
}
