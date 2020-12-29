using Cauldron.Grpc.Api;
using Cauldron.Grpc.Models;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CauldronSimplePlayer_wpf
{
    class Client
    {
        private static readonly Random random = new Random();

        public static T RandomPick<T>(IReadOnlyList<T> source) => source.Any() ? source[Client.random.Next(source.Count)] : default;

        public static bool CanPutFieldCard(Card card)
        {
            return card.CardType == CardDef.Types.Type.Artifact
                || card.CardType == CardDef.Types.Type.Creature
                || card.CardType == CardDef.Types.Type.Token
                ;
        }

        public static bool IsPlayable(GameContext context, Card card)
        {
            // フィールドに出すカードはフィールドに空きがないとプレイできない
            var fieldIsFull = context.You.PublicPlayerInfo.Field.Count
                == context.RuleBook.MaxNumFieldCars;
            if (CanPutFieldCard(card) && fieldIsFull)
            {
                return false;
            }

            // コストが払えないとプレイできない
            var usableMp = context.You.PublicPlayerInfo.CurrentMp;
            if (usableMp < card.Cost)
            {
                return false;
            }

            //// 各カードごとの条件
            //if (!card.Require.IsPlayable(environment))
            //{
            //    return false;
            //}

            return true;
        }

        public static bool IsSummoningSickness(Card card) => card.TurnNumInField < 1;

        public static bool CanAttack(Card attackCard)
        {
            return attackCard != null
                // クリーチャーでなければ攻撃できない
                && attackCard.CardType == CardDef.Types.Type.Creature
                // 召喚酔いでない
                && !IsSummoningSickness(attackCard)
                // 攻撃不能状態でない
                && !attackCard.Abilities.Contains(CardDef.Types.Ability.CantAttack)
                ;
        }

        public static bool CanAttack(Card attackCard, PublicPlayerInfo guardPlayer)
        {
            var existsCover = guardPlayer.Field
                .Any(c => c.Abilities.Contains(CardDef.Types.Ability.Cover)
                    && !c.Abilities.Contains(CardDef.Types.Ability.Stealth));

            return
                // 攻撃可能なカード
                CanAttack(attackCard)
                // 持ち主には攻撃できない
                && attackCard.OwnerId != guardPlayer.Id
                // カバーされていない
                && !existsCover
                ;
        }

        public static bool CanAttack(Card attackCard, Card guardCard, GameContext environment)
        {
            var guardPlayer = environment.Opponent;
            var existsCover = guardPlayer.Field
                .Any(c => c.Abilities.Contains(CardDef.Types.Ability.Cover)
                    && !c.Abilities.Contains(CardDef.Types.Ability.Stealth));

            var coverCheck = guardCard.Abilities.Contains(CardDef.Types.Ability.Cover)
                || !existsCover;

            return
                // 攻撃可能なカード
                CanAttack(attackCard)
                // 自分自信のカードには攻撃できない
                && attackCard.OwnerId != guardCard.OwnerId
                // クリーチャー以外には攻撃できない
                && guardCard.CardType == CardDef.Types.Type.Creature
                // ステルス状態は攻撃対象にならない
                && !guardCard.Abilities.Contains(CardDef.Types.Ability.Stealth)
                // カバー関連のチェック
                && coverCheck
                ;
        }

        private readonly Cauldron.Grpc.Api.Cauldron.CauldronClient grpcClient;
        public readonly string PlayerName;
        private readonly Action<ReadyGameReply> onPushedFromServerAction;

        public string GameId { get; private set; }
        public string PlayerId { get; private set; }

        public GameContext CurrentContext { get; private set; }

        public Client(string playerName, Action<ReadyGameReply> onPushNotifyAction)
            : this(playerName, "", onPushNotifyAction)
        { }

        public Client(string playerName, string gameId, Action<ReadyGameReply> onPushedFromServerAction)
        {
            this.PlayerName = playerName;
            this.GameId = gameId;
            this.onPushedFromServerAction = onPushedFromServerAction;

            var channel = GrpcChannel.ForAddress("https://localhost:5001");
            this.grpcClient = new Cauldron.Grpc.Api.Cauldron.CauldronClient(channel);
        }

        public async ValueTask<bool> PlayActionAsync(Func<ValueTask> action)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5));

            if (this.CurrentContext?.GameOver ?? false)
            {
                var winner = this.CurrentContext.WinnerPlayerId == this.PlayerId
                    ? this.CurrentContext.You.PublicPlayerInfo.Name
                    : this.CurrentContext.Opponent.Name;
                Console.WriteLine($"{winner} の勝ち！");
                return false;
            }

            await action();

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>ゲームID</returns>
        public async ValueTask<string> OpenNewGameAsync()
        {
            Console.WriteLine("OpenNewGame: " + this.PlayerName);

            var ruleBook = new RuleBook()
            {
                InitialMp = 1,
                MaxMp = 10,
                MinMp = 0,
                MpByStep = 1,
                InitialNumHands = 5,
                MaxNumHands = 10,
                InitialPlayerHp = 10,
                MaxPlayerHp = 10,
                MinPlayerHp = 0,
                MaxNumDeckCards = 40,
                MinNumDeckCards = 40,
                MaxNumFieldCars = 5,
            };

            var reply = await this.grpcClient.OpenNewGameAsync(new OpenNewGameRequest()
            {
                RuleBook = ruleBook,
            });

            this.GameId = reply.GameId;

            return this.GameId;
        }

        public async ValueTask EnterGameAsync()
        {
            var cardPoolReply = await this.grpcClient.GetCardPoolAsync(new GetCardPoolRequest()
            {
                GameId = this.GameId
            });
            var cardPool = cardPoolReply.Cards
                .Where(c => !c.IsToken)
                .ToArray();

            var deckCardIds = Enumerable.Range(0, 40)
                .Select(_ => Client.RandomPick(cardPool).Id);

            var reply = await this.grpcClient.EnterGameAsync(new EnterGameRequest()
            {
                GameId = this.GameId,
                PlayerName = this.PlayerName,
                DeckCardIds = { deckCardIds }
            });

            this.PlayerId = reply.PlayerId;
        }

        public void ReadyGame()
        {
            var call = this.grpcClient.ReadyGame(new ReadyGameRequest()
            {
                GameId = this.GameId,
                PlayerId = this.PlayerId
            });

            var source = new CancellationTokenSource();
            var token = source.Token;

            Task.Run(async () =>
            {
                while (true)
                {
                    if (await call.ResponseStream.MoveNext(token))
                    {
                        var reply = call.ResponseStream.Current;

                        this.onPushedFromServerAction(reply);
                    }
                }
            });
        }

        public async ValueTask StartTurnAsync()
        {
            var reply = await this.grpcClient.StartTurnAsync(new StartTurnRequest()
            {
                GameId = this.GameId,
                PlayerId = this.PlayerId
            });

            this.CurrentContext = reply.GameContext;
        }

        public async ValueTask PlayFromHandAsync(string cardId)
        {
            var reply = await this.grpcClient.PlayFromHandAsync(new PlayFromHandRequest()
            {
                GameId = this.GameId,
                PlayerId = this.PlayerId,
                HandCardId = cardId
            });

            this.CurrentContext = reply.GameContext;
        }

        public async ValueTask AttackToCardAsync(string attackCardId, string guardCardId)
        {
            var reply = await this.grpcClient.AttackToCreatureAsync(new AttackToCreatureRequest()
            {
                GameId = this.GameId,
                PlayerId = this.PlayerId,
                AttackHandCardId = attackCardId.ToString(),
                GuardHandCardId = guardCardId.ToString()
            });

            this.CurrentContext = reply.GameContext;
        }

        public async ValueTask AttackToPlayerAsync(string attackCardId)
        {
            var reply = await this.grpcClient.AttackToPlayerAsync(new AttackToPlayerRequest()
            {
                GameId = this.GameId,
                PlayerId = this.PlayerId,
                AttackHandCardId = attackCardId,
                GuardPlayerId = this.CurrentContext.Opponent.Id,
            });
            this.CurrentContext = reply.GameContext;
        }

        public async ValueTask EndTurnAsync()
        {
            var reply = await this.grpcClient.EndTurnAsync(new EndTurnRequest()
            {
                GameId = this.GameId,
                PlayerId = this.PlayerId
            });

            this.CurrentContext = reply.GameContext;
        }
    }
}
