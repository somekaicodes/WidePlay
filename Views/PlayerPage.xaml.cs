using WidePlay.ViewModels;

namespace WidePlay.Views;

public partial class PlayerPage : ContentPage
{
    public PlayerPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
