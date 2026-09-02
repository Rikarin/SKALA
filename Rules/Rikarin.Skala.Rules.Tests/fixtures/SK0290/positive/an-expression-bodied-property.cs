public sealed class ExpressionBodiedProperty {
    readonly int count = 3;

    public int? Count => new int?(count);
}
