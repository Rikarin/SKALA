class C {
    bool IsBlank(string? text) => text is null | text!.Length == 0;
}
