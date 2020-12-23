using System.Windows.Controls;

namespace CauldronSimplePlayer_wpf
{
    /// <summary>
    /// FieldCardControl.xaml の相互作用ロジック
    /// </summary>
    public partial class FieldCardControl : UserControl
    {
        public FieldCardControl()
        {
            InitializeComponent();
        }

        private void StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.DataContext is FieldCardControlViewmodel card)
            {
                card.ClickCommand();
            }
        }
    }
}
