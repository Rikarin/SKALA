using System;

sealed class Renderer {
    public void Unnamed(Model model) {
        try {
            Render(model.Header);
        } catch (NullReferenceException) {
            Render("(none)");
        }
    }

    public void Named(Model model) {
        try {
            Render(model.Header);
        } catch (NullReferenceException error) {
            Console.WriteLine(error);
        }
    }

    public void Qualified(Model model) {
        try {
            Render(model.Header);
        } catch (System.NullReferenceException) {
            Render("(none)");
        }
    }

    public void GloballyQualified(Model model) {
        try {
            Render(model.Header);
        } catch (global::System.NullReferenceException) {
            Render("(none)");
        }
    }

    // A filter narrows which occurrences are handled; it does not stop the clause naming the type.
    public void Filtered(Model model, bool tolerant) {
        try {
            Render(model.Header);
        } catch (NullReferenceException) when (tolerant) {
            Render("(none)");
        }
    }

    static void Render(string text) { }
}

sealed class Model {
    public string Header { get; set; } = "";
}
