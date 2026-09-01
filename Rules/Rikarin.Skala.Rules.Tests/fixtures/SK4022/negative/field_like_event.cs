using System;

struct EventFixture {
    public event Action? Changed;

    public void Raise() => Changed?.Invoke();
}
