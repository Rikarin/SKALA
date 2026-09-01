using System;

sealed class SetterValueFixture {
    Func<string>? formatter;

    public string Text {
        set => formatter = () => value;
    }
}
