using Reactive.Bindings;
using System.Windows.Media;

namespace CauldronSimplePlayer_wpf
{
    class DummyCardControlViewmodel
    {
        public ReactiveProperty<string> DisplayText { get; } = new("");

        public ReactiveProperty<Brush> Color { get; } = new(Brushes.Black);

        public DummyCardControlViewmodel(string displayText)
        {
            this.DisplayText.Value = displayText;
        }
    }
}
