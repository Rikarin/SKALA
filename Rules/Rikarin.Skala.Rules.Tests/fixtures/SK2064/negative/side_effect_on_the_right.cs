// ⚠ Written this way so both validators run and both messages are collected. Short-circuiting it
// deletes work, which is why `&` on booleans is not always a mistake.
class C {
    readonly System.Collections.Generic.List<string> messages = new();

    bool Validate(string name, int age) => ValidateName(name) & ValidateAge(age);

    bool ValidateName(string name) {
        if (name.Length == 0) {
            messages.Add("name is empty");
            return false;
        }

        return true;
    }

    bool ValidateAge(int age) {
        if (age < 0) {
            messages.Add("age is negative");
            return false;
        }

        return true;
    }
}
