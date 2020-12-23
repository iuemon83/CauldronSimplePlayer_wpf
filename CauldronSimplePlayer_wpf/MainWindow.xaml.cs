using System.Windows;

namespace CauldronSimplePlayer_wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private readonly MainWindowsViewmodel vm = new();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = this.vm;
            this.vm.LogViewerScrollToBottom = () =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.LogScrollViewer.ScrollToBottom();
                });
            };
        }

        private async void GameStartButton_Click(object sender, RoutedEventArgs e)
        {
            await this.vm.GameStartCommand();
        }

        private async void AttackToPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            await this.vm.OnAttackPlayerCommand();
        }

        private async void EndTurnButton_Click(object sender, RoutedEventArgs e)
        {
            await this.vm.TurnEndCommand();
        }
    }
}
