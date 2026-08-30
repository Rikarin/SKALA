// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class C {
    void M(object value, int count, string[] items) {
        while (!cancellationSignal.IsCancellationRequested
               && bufferedReader.TryRead(out var next)
               && next.Length > count) {
            Use(next);
        }

        foreach (var item in candidateRegistry.Where(candidate => candidate.Kind == BindingKind.Texture)
                     .Select(Project)) {
            Use(item);
        }

        if (ReflectionUtils.ImplementsGenericDefinition(
                NonNullableUnderlyingTypeName,
                typeof(IEnumerable<>),
                out var found
            )) {
            Use(found);
        }

        try {
            Use(value);
        } catch (Exception exception) when (exception is BindingResolutionException
                                                or DocumentParseException
                                                or NotSupportedException) {
            Use(exception);
        }
    }
}
