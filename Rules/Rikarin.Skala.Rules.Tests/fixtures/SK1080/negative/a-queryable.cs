using System.Linq;

public sealed class Registry {
    // A provider is free to translate one spelling of a pipeline and not the other.
    public static IQueryable<string> Names(IQueryable<object> values) =>
        values.Where(value => value is string).Cast<string>();
}
