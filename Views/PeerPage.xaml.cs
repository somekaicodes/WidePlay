using WidePlay.ViewModels;

namespace WidePlay.Views;

public partial class PeerPage : ContentPage
{
    public PeerPage(PeerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
