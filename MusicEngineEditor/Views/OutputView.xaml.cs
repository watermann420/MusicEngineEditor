using System.Windows.Controls;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

public partial class OutputView : UserControl
{
    public OutputView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            if (DataContext is OutputViewModel vm)
            {
                vm.OutputChanged += OnOutputChanged;
            }
        };
    }

    private void OnOutputChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is OutputViewModel vm && vm.AutoScroll)
        {
            OutputTextBox.ScrollToEnd();
        }
    }
}
