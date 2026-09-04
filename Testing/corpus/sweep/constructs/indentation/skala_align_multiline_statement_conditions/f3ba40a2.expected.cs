// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
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
