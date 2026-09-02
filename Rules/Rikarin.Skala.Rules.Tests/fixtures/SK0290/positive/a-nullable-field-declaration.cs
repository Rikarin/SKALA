public sealed class FieldDeclaration {
    readonly int? limit = new int?(10);

    public bool HasLimit() => limit.HasValue;
}
