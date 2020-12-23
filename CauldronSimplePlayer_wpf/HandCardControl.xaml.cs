using System.Windows;
using System.Windows.Controls;

namespace CauldronSimplePlayer_wpf
{
    /// <summary>
    /// HandCardControl.xaml の相互作用ロジック
    /// </summary>
    public partial class HandCardControl : UserControl
    {
        public HandCardControl()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is HandCardControlViewmodel card)
            {
                card.PlayButtonClickCommand();
            }
        }
    }
}
