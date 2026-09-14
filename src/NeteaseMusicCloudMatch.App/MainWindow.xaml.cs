using NeteaseMusicCloudMatch.App.Services;
using NeteaseMusicCloudMatch.App.ViewModels;
using Wpf.Ui.Controls;

namespace NeteaseMusicCloudMatch.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(
        MainViewModel viewModel,
        Wpf.Ui.IContentDialogService contentDialogs,
        Wpf.Ui.ISnackbarService snackbars,
        NeteaseMusicCloudMatch.App.Services.IThemeService themeService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        contentDialogs.SetDialogHost(RootDialogHost);
        snackbars.SetSnackbarPresenter(RootSnackbarPresenter);
        Loaded += (_, _) => themeService.AttachWindow(this);
    }

    public MainViewModel ViewModel { get; }
}
