using System;

class C {
    Action M() => delegate { M(); };
}
