using Cauldron.Grpc.Models;
using Reactive.Bindings;
using System;
using System.Linq;
using System.Windows.Media;

namespace CauldronSimplePlayer_wpf
{
    class HandCardControlViewmodel
    {
        public readonly Card Card;
        private readonly Action<HandCardControlViewmodel> onPlayAction;

        public bool IsEmpty => this.Card == null;

        public ReactiveProperty<string> DisplayText { get; } = new("");

        public ReactiveProperty<Brush> Color { get; } = new(Brushes.Black);

        public HandCardControlViewmodel(Card card,
            Action<HandCardControlViewmodel> onPlayAction)
        {
            this.Card = card;
            this.onPlayAction = onPlayAction;

            this.DisplayText.Value = this.Card == null
                ? ""
                : @$"
({this.Card.Cost})
{this.Card.Name}
{(card.CardType == CardDef.Types.Type.Creature ? $"[{this.Card.Power} / {this.Card.Toughness}]" : "")}
{string.Join(", ", card.Abilities.Select(a => a.ToString()))}
";
        }

        public void PlayButtonClickCommand()
        {
            this.onPlayAction(this);
        }
    }
}
