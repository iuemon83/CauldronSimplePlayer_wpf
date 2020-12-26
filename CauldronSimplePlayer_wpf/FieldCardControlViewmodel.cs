using Cauldron.Grpc.Models;
using Reactive.Bindings;
using System;
using System.Windows.Media;

namespace CauldronSimplePlayer_wpf
{
    class FieldCardControlViewmodel
    {
        public readonly Card Card;
        private readonly Action<FieldCardControlViewmodel> onClickAction;

        public bool IsEmpty => this.Card == null;

        public ReactiveProperty<string> DisplayText { get; } = new("");

        public ReactiveProperty<Brush> Color { get; } = new(Brushes.Black);

        public FieldCardControlViewmodel(Card card,
            Action<FieldCardControlViewmodel> onClickAction)
        {
            this.Card = card;
            this.onClickAction = onClickAction;

            this.DisplayText.Value = this.Card == null
                ? ""
                : @$"
({this.Card.Cost})
{this.Card.Name}
{(card.CardType == CardDef.Types.Type.Creature ? $"[{this.Card.Power} / {this.Card.Toughness}]" : "")}
";
        }

        public void ClickCommand()
        {
            this.onClickAction(this);
        }
    }
}
