using Cauldron.Grpc.Api;
using Cauldron.Grpc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CauldronSimplePlayer_wpf
{
    /// <summary>
    /// AI 用
    /// </summary>
    class AiClient
    {
        private static readonly Random random = new Random();

        public static T RandomPick<T>(IReadOnlyList<T> source) => source.Any() ? source[AiClient.random.Next(source.Count)] : default;

        public readonly Client Client;

        public AiClient(string playerName, string gameId, Action<ReadyGameReply> onPushedFromServerAction)
        {
            this.Client = new Client(playerName, gameId, onPushedFromServerAction);
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                await this.Client.EnterGameAsync();
                this.Client.ReadyGame();
            });
        }

        public async ValueTask PlayTurn()
        {
            await this.Client.PlayActionAsync(() => this.Client.StartTurnAsync());
            await this.Client.PlayActionAsync(() => this.PlayFromHandAsync());
            await this.Client.PlayActionAsync(() => this.AttackAsync());
            await this.Client.PlayActionAsync(() => this.Client.EndTurnAsync());
        }

        public async ValueTask PlayFromHandAsync()
        {
            while (true)
            {
                var useableMp = this.Client.CurrentContext.You.PublicPlayerInfo.CurrentMp;
                var card = RandomPick(this.Client.CurrentContext.You.Hands
                    .Where(c => Client.IsPlayable(this.Client.CurrentContext, c))
                    .ToArray());

                if (card == default)
                {
                    return;
                }

                await this.Client.PlayFromHandAsync(card.Id);
            }
        }

        public async ValueTask AttackAsync()
        {
            // フィールドのすべてのカードで敵に攻撃
            var allCreatures = this.Client.CurrentContext
                .You.PublicPlayerInfo.Field
                .Where(card => card.CardType == CardDef.Types.Type.Creature
                    || card.CardType == CardDef.Types.Type.Token);

            foreach (var attackCard in allCreatures)
            {
                var canTargetCards = this.Client.CurrentContext.Opponent.Field
                    .Where(opponentCard => Client.CanAttack(attackCard, opponentCard, this.Client.CurrentContext))
                    .ToArray();

                var canAttackToPlayer = Client.CanAttack(attackCard, this.Client.CurrentContext.Opponent);
                var canAttackToCreature = canTargetCards.Any();

                // 敵のモンスターがいる
                if (canAttackToCreature && random.Next(100) > 50)
                {
                    var opponentCardId = canTargetCards[0].Id;

                    await this.Client.AttackToCardAsync(attackCard.Id, opponentCardId);
                }
                else if (canAttackToPlayer)
                {
                    await this.Client.AttackToPlayerAsync(attackCard.Id);
                }
            }
        }
    }
}
