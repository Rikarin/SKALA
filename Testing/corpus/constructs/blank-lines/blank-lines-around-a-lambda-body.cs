using System;

class C {
    Action M() =>
        () => {
            M();
        };
}
