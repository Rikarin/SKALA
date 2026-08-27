using System.Threading.Tasks;

public abstract class Base {
    public virtual async void Start() {
        await Task.Delay(1);
    }
}

public sealed class Derived : Base {
    public override async void Start() {
        await Task.Delay(2);
    }
}
