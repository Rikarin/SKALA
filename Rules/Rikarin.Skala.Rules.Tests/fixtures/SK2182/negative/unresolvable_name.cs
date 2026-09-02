static class Plugins {
    // The only option for a type this assembly deliberately does not reference. The name does not
    // resolve here, so there is nothing to suggest and nothing is reported.
    public static bool IsHandler(object instance) => instance.GetType().Name == "AcmeLegacyHandler";
}
