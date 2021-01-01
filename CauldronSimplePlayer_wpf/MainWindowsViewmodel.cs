using Cauldron.Grpc.Api;
using Cauldron.Grpc.Models;
using Grpc.Core;
using Reactive.Bindings;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CauldronSimplePlayer_wpf
{
    class MainWindowsViewmodel
    {
        private static string GetPlayerName(GameContext gameContext, string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return "";

            return gameContext.You.PublicPlayerInfo.Id == playerId
                ? gameContext.You.PublicPlayerInfo.Name
                : gameContext.Opponent.Name;
        }

        private static (string ownerPlayerName, string cardName) GetCardName(GameContext gameContext, Zone zone, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return ("", "");

            var zonePlayer = gameContext.You.PublicPlayerInfo.Id == zone.PlayerId
                ? gameContext.You.PublicPlayerInfo
                : gameContext.Opponent;

            var card = zone.ZoneName switch
            {
                "Hand" => gameContext.You.PublicPlayerInfo.Id == zone.PlayerId
                    ? gameContext.You.Hands.First(c => c.Id == cardId)
                    : null,
                "Field" => zonePlayer.Field.First(c => c.Id == cardId),
                "Cemetery" => zonePlayer.Cemetery.First(c => c.Id == cardId),
                _ => null
            };

            if (card == null) return ("", "");

            var ownerName = GetPlayerName(gameContext, card.OwnerId);

            return (ownerName, card.Name);
        }

        private static (string ownerPlayerName, string cardName) GetCardName(GameContext gameContext, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return ("", "");

            var card = gameContext.You.Hands
                .Concat(gameContext.You.PublicPlayerInfo.Field)
                .Concat(gameContext.You.PublicPlayerInfo.Cemetery)
                .Concat(gameContext.Opponent.Field)
                .Concat(gameContext.Opponent.Cemetery)
                .First(c => c.Id == cardId);

            var ownerName = GetPlayerName(gameContext, card.OwnerId);

            return (ownerName, card.Name);
        }

        public ReactiveProperty<string> GameId { get; } = new("");

        public ReactiveProperty<string> PlayerName { get; } = new("ryu");

        public ReactiveProperty<string> Log { get; } = new("");

        public ReactiveProperty<string> YouActiveSymbol { get; } = new("");
        public ReactiveProperty<string> OpponentActiveSymbol { get; } = new("");

        public ReactiveProperty<PrivatePlayerInfo> You { get; } = new();
        public ReactiveProperty<PublicPlayerInfo> Opponent { get; } = new();

        public ReadOnlyReactiveProperty<FieldCardControlViewmodel[]> YouField { get; }
        public ReadOnlyReactiveProperty<HandCardControlViewmodel[]> YouHands1 { get; }
        public ReadOnlyReactiveProperty<HandCardControlViewmodel[]> YouHands2 { get; }
        public ReadOnlyReactiveProperty<DummyCardControlViewmodel> YouDeck { get; }
        public ReadOnlyReactiveProperty<DummyCardControlViewmodel> YouCemetery { get; }

        public ReadOnlyReactiveProperty<FieldCardControlViewmodel[]> OpponentField { get; }
        public ReadOnlyReactiveProperty<DummyCardControlViewmodel> OpponentHand { get; }
        public ReadOnlyReactiveProperty<DummyCardControlViewmodel> OpponentDeck { get; }
        public ReadOnlyReactiveProperty<DummyCardControlViewmodel> OpponentCemetery { get; }

        private Client client;

        public Action LogViewerScrollToBottom { get; set; }

        private FieldCardControlViewmodel attackCard;

        public MainWindowsViewmodel()
        {
            this.You = new ReactiveProperty<PrivatePlayerInfo>();
            this.Opponent = new ReactiveProperty<PublicPlayerInfo>();

            this.YouField = this.You
                .Select(x => x?.PublicPlayerInfo?.Field?.Concat(Enumerable.Repeat(default(Card), 5))?.Take(5).ToArray() ?? Array.Empty<Card>())
                .Select(x => x.Select(c => new FieldCardControlViewmodel(c, async c => await this.OnClickField(c))).ToArray())
                .ToReadOnlyReactiveProperty();

            this.YouHands1 = this.You
                .Select(x => x?.Hands?.Concat(Enumerable.Repeat(default(Card), 5))?.Take(5).ToArray() ?? Array.Empty<Card>())
                .Select(x => x.Select(c => new HandCardControlViewmodel(c, async c => await this.OnPlayHand(c))).ToArray())
                .ToReadOnlyReactiveProperty();

            this.YouHands2 = this.You
                .Select(x => x?.Hands?.Concat(Enumerable.Repeat(default(Card), 10))?.Skip(5).Take(5).ToArray() ?? Array.Empty<Card>())
                .Select(x => x.Select(c => new HandCardControlViewmodel(c, async c => await this.OnPlayHand(c))).ToArray())
                .ToReadOnlyReactiveProperty();

            this.YouDeck = this.You
                .Select(x => new DummyCardControlViewmodel(x?.PublicPlayerInfo?.NumDeckCards.ToString()))
                .ToReadOnlyReactiveProperty();

            this.YouCemetery = this.You
                .Select(x => new DummyCardControlViewmodel(x?.PublicPlayerInfo?.Cemetery.Count.ToString()))
                .ToReadOnlyReactiveProperty();

            this.OpponentField = this.Opponent
                .Select(x => x?.Field?.Concat(Enumerable.Repeat(default(Card), 5))?.Take(5).ToArray() ?? Array.Empty<Card>())
                .Select(x => x.Select(c => new FieldCardControlViewmodel(c, async c => await this.OnClickField(c))).ToArray())
                .ToReadOnlyReactiveProperty();

            this.OpponentHand = this.Opponent
                .Select(x => new DummyCardControlViewmodel(x?.NumHands.ToString()))
                .ToReadOnlyReactiveProperty();

            this.OpponentDeck = this.Opponent
                .Select(x => new DummyCardControlViewmodel(x?.NumDeckCards.ToString()))
                .ToReadOnlyReactiveProperty();

            this.OpponentCemetery = this.Opponent
                .Select(x => new DummyCardControlViewmodel(x?.Cemetery.Count.ToString()))
                .ToReadOnlyReactiveProperty();
        }

        private async ValueTask OnPlayHand(HandCardControlViewmodel hand)
        {
            if (hand.IsEmpty) return;

            if (hand.Card != null)
            {
                // 被攻撃カードの選択処理
                await this.client.PlayFromHandAsync(hand.Card.Id);
                this.ApplyGameContext(this.client.CurrentContext);
            }
        }

        private async ValueTask OnClickField(FieldCardControlViewmodel field)
        {
            if (field.IsEmpty) return;

            if (field.Card.OwnerId == this.client.PlayerId)
            {
                // 攻撃カードの選択処理

                if (this.attackCard != null)
                {
                    var prevAttackCardId = this.attackCard.Card.Id;

                    // すでに選択済みのものをもとに戻す
                    this.attackCard.Color.Value = Brushes.Black;
                    this.attackCard = null;

                    // 同じカードをクリックしたら解除するだけ
                    if (prevAttackCardId == field.Card.Id)
                    {
                        return;
                    }
                }

                this.attackCard = field;
                this.attackCard.Color.Value = Brushes.Red;
            }
            else
            {
                if (this.attackCard != null)
                {
                    // 被攻撃カードの選択処理
                    await this.client.AttackToCardAsync(this.attackCard.Card.Id, field.Card.Id);
                    this.ApplyGameContext(this.client.CurrentContext);

                    this.attackCard.Color.Value = Brushes.Black;
                    this.attackCard = null;
                }
            }
        }

        public async ValueTask OnAttackPlayerCommand()
        {
            if (this.attackCard != null)
            {
                // 被攻撃カードの選択処理
                await this.client.AttackToPlayerAsync(this.attackCard.Card.Id);
                this.ApplyGameContext(this.client.CurrentContext);

                this.attackCard.Color.Value = Brushes.Black;
                this.attackCard = null;
            }
        }

        public async Task GameStartCommand()
        {
            if (this.PlayerName.Value == "")
            {
                return;
            }

            try
            {
                if (this.GameId.Value == "")
                {
                    this.client = new Client(this.PlayerName.Value, this.OnPushedFromServer);
                    this.GameId.Value = await this.client.OpenNewGameAsync();
                }
                else
                {
                    this.client = new Client(this.PlayerName.Value, this.GameId.Value, this.OnPushedFromServer);
                }
            }
            catch (RpcException)
            {
                this.Logging("サーバーへの接続に失敗しました。");
                return;
            }

            await this.client.EnterGameAsync();
            this.client.ReadyGame();

            // AI敵
            this.StartAiPlayer();
        }

        private void StartAiPlayer()
        {
            AiClient aiClient = null;
            aiClient = new AiClient("AI", this.GameId.Value, async reply =>
            {
                switch (reply.Code)
                {
                    case ReadyGameReply.Types.Code.StartGame:
                        {
                            this.Logging($"ゲーム開始: {aiClient.Client.PlayerName}");
                            break;
                        }

                    case ReadyGameReply.Types.Code.StartTurn:
                        {
                            this.Logging($"ターン開始: {aiClient.Client.PlayerName}");

                            this.YouActiveSymbol.Value = "";
                            this.OpponentActiveSymbol.Value = "●";

                            await aiClient.PlayTurn();
                            break;
                        }
                }
            });

            aiClient.Start();
        }

        private async void OnPushedFromServer(ReadyGameReply reply)
        {
            var newGameContext = reply.GameContext;
            switch (reply.Code)
            {
                case ReadyGameReply.Types.Code.StartGame:
                    {
                        this.Logging($"ゲーム開始: {this.client.PlayerName}");
                        break;
                    }

                case ReadyGameReply.Types.Code.StartTurn:
                    {
                        // 自分のターン
                        this.Logging($"ターン開始: {this.client.PlayerName}");

                        this.YouActiveSymbol.Value = "●";
                        this.OpponentActiveSymbol.Value = "";

                        await this.client.StartTurnAsync();
                        newGameContext = this.client.CurrentContext;

                        break;
                    }

                case ReadyGameReply.Types.Code.GameOver:
                    {
                        this.Logging($"ゲーム終了: {this.client.PlayerName}");
                        break;
                    }

                case ReadyGameReply.Types.Code.AddCard:
                    {
                        var notify = reply.AddCardNotify;

                        var (ownerName, cardName) = GetCardName(reply.GameContext, notify.ToZone, notify.CardId);
                        var playerName = GetPlayerName(reply.GameContext, notify.ToZone.PlayerId);

                        this.Logging($"追加: {cardName}({ownerName}) to {notify.ToZone.ZoneName}({playerName})");
                        break;
                    }

                case ReadyGameReply.Types.Code.MoveCard:
                    {
                        var notify = reply.MoveCardNotify;

                        var (ownerName, cardName) = GetCardName(reply.GameContext, notify.ToZone, notify.CardId);
                        var playerName = GetPlayerName(reply.GameContext, notify.ToZone.PlayerId);

                        this.Logging($"移動: {cardName}({ownerName}) to {notify.ToZone.ZoneName}({playerName})");
                        break;
                    }

                case ReadyGameReply.Types.Code.ModifyCard:
                    {
                        var notify = reply.ModifyCardNotify;

                        var (ownerName, cardName) = GetCardName(reply.GameContext, notify.CardId);

                        this.Logging($"修整: {cardName}({ownerName})");
                        break;
                    }

                case ReadyGameReply.Types.Code.ModifyPlayer:
                    {
                        var notify = reply.ModifyPlayerNotify;

                        var playerName = GetPlayerName(reply.GameContext, notify.PlayerId);

                        this.Logging($"修整: {playerName}");
                        break;
                    }

                case ReadyGameReply.Types.Code.Damage:
                    {
                        var notify = reply.DamageNotify;

                        var sourceCard = GetCardName(reply.GameContext, notify.SourceCardId);
                        var guardCard = GetCardName(reply.GameContext, notify.GuardCardId);
                        var guardPlayerName = GetPlayerName(reply.GameContext, notify.GuardPlayerId);

                        this.Logging($"ダメージ: {sourceCard.cardName}({sourceCard.ownerPlayerName}) > {guardCard.cardName}({guardCard.ownerPlayerName}){guardPlayerName} {notify.Damage}");
                        break;
                    }

                default:
                    this.Logging(reply.Code.ToString());
                    break;
            }

            if (newGameContext != null)
            {
                this.ApplyGameContext(newGameContext);
            }
        }

        private void ApplyGameContext(GameContext context)
        {
            this.You.Value = context.You;
            this.Opponent.Value = context.Opponent;

            if (context.GameOver)
            {
                this.YouActiveSymbol.Value = this.client.PlayerId == context.WinnerPlayerId
                    ? "Win!!"
                    : "Lose...";

                this.OpponentActiveSymbol.Value = this.client.PlayerId == context.WinnerPlayerId
                    ? "Lose..."
                    : "Win!!";
            }
        }

        private void Logging(string message)
        {
            this.Log.Value += message + Environment.NewLine;

            this.LogViewerScrollToBottom();
        }

        public async ValueTask TurnEndCommand()
        {
            await this.client.EndTurnAsync();
            this.ApplyGameContext(this.client.CurrentContext);

            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }
    }
}
