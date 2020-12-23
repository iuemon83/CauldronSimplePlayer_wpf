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
                .Select(x => new DummyCardControlViewmodel(x?.PublicPlayerInfo?.DeckCount.ToString()))
                .ToReadOnlyReactiveProperty();

            this.YouCemetery = this.You
                .Select(x => new DummyCardControlViewmodel(x?.PublicPlayerInfo?.CemeteryCount.ToString()))
                .ToReadOnlyReactiveProperty();

            this.OpponentField = this.Opponent
                .Select(x => x?.Field?.Concat(Enumerable.Repeat(default(Card), 5))?.Take(5).ToArray() ?? Array.Empty<Card>())
                .Select(x => x.Select(c => new FieldCardControlViewmodel(c, async c => await this.OnClickField(c))).ToArray())
                .ToReadOnlyReactiveProperty();

            this.OpponentHand = this.Opponent
                .Select(x => new DummyCardControlViewmodel(x?.HandCount.ToString()))
                .ToReadOnlyReactiveProperty();

            this.OpponentDeck = this.Opponent
                .Select(x => new DummyCardControlViewmodel(x?.DeckCount.ToString()))
                .ToReadOnlyReactiveProperty();

            this.OpponentCemetery = this.Opponent
                .Select(x => new DummyCardControlViewmodel(x?.CemeteryCount.ToString()))
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
            Client aiClient = null;
            aiClient = new Client("AI", this.GameId.Value, async reply =>
            {
                switch (reply.Code)
                {
                    case ReadyGameReply.Types.Code.StartGame:
                        {
                            this.Logging($"ゲーム開始: {aiClient.PlayerName}");
                            break;
                        }

                    case ReadyGameReply.Types.Code.StartTurn:
                        {
                            this.Logging($"ターン開始: {aiClient.PlayerName}");

                            this.YouActiveSymbol.Value = "";
                            this.OpponentActiveSymbol.Value = "●";

                            await aiClient.PlayActionAsync(() => aiClient.StartTurnAsync());
                            await aiClient.PlayActionAsync(() => aiClient.PlayFromHandAsync());
                            await aiClient.PlayActionAsync(() => aiClient.AttackAsync());
                            await aiClient.PlayActionAsync(() => aiClient.EndTurnAsync());
                            break;
                        }
                }
            });

            Task.Run(async () =>
            {
                await aiClient.EnterGameAsync();
                aiClient.ReadyGame();
            });
        }

        private async void OnPushedFromServer(ReadyGameReply reply)
        {
            this.Logging(reply.Code.ToString());

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

                        await this.client.PlayActionAsync(() => this.client.StartTurnAsync());
                        newGameContext = this.client.CurrentContext;

                        //await this.client.PlayActionAsync(() => this.client.PlayFromHandAsync());
                        //this.ApplyGameContext(this.client.CurrentContext);

                        //await this.client.PlayActionAsync(() => this.client.AttackAsync());
                        //this.ApplyGameContext(this.client.CurrentContext);

                        //await this.client.PlayActionAsync(() => this.client.EndTurnAsync());
                        //this.ApplyGameContext(this.client.CurrentContext);

                        break;
                    }

                case ReadyGameReply.Types.Code.GameOver:
                    {
                        this.Logging($"ゲーム終了: {this.client.PlayerName}");
                        break;
                    }
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
            await this.client.PlayActionAsync(() => this.client.EndTurnAsync());
            this.ApplyGameContext(this.client.CurrentContext);

            await Task.Delay(TimeSpan.FromSeconds(0.5));
        }
    }
}
