// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-09
// Fuzz 14071685607328961301: a body with no break point of its own is read through by the arm's head only up
// to thirteen columns; a wider one breaks after the arrow, whatever the head's width (measured 2026-10-09).

class C {
    object M(object value) =>
        value switch {
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } =>
                "sssssssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "ssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "sssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "ssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "sssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "ssssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "sssssssssss",
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } =>
                "ssssssssssss",
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } =>
                "sssssssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "ssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "sssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "ssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "sssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "ssssssssss",
            {
                Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss"
            } => "sssssssssss",
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } =>
                "ssssssssssss",
            { Length: > 0, Name: "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" } =>
                "sssssssssssss",
            _ => 0
        };
}
