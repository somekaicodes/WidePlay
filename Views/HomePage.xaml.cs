using WidePlay.ViewModels;

namespace WidePlay.Views;

public partial class HomePage : ContentPage
{
    // ViewModel is injected by DI and set as the binding context for the XAML.
    public HomePage(SessionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
