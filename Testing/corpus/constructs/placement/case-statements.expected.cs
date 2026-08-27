// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
class CaseStatements {
    void M(int v) {
        switch (v) {
            case 1: OnTheSameLine(); break;
            case 2:
                OnItsOwnLine();
                break;
            case 3: {
                InABlock();
                break;
            }
        }
    }
}
