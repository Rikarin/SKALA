using System;

public sealed class Loader {
    public void Load(string path) {
        Validate(path);

        void Validate(string candidate) {
            if (candidate is null) {
                throw new ArgumentNullException("candidate");
            }
        }
    }
}
