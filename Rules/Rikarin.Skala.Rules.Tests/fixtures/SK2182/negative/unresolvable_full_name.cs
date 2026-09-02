static class Plugins {
    public static bool IsHandler(object instance) =>
        instance.GetType().FullName == "Acme.Legacy.Handler";
}
