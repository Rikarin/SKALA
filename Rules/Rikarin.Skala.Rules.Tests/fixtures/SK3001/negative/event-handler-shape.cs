using System;
using System.Threading.Tasks;

public sealed class View {
    public async void HandleClicked(object sender, EventArgs e) {
        await Task.Delay(1);
    }
}
