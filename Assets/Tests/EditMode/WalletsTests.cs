using Arreo.Economy;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    /// <summary>D75: the hato's money is shared by default; a game can be created with money per player.</summary>
    public class WalletsTests
    {
        [Test]
        public void Shared_EveryPlayerUsesTheHatoWallet()
        {
            var wallets = new Wallets(MoneyMode.Shared);

            Assert.That(wallets.For(1), Is.SameAs(wallets.Hato));
            Assert.That(wallets.For(2), Is.SameAs(wallets.Hato));
            Assert.That(wallets.Hato.Owner.IsHato, Is.True);
        }

        [Test]
        public void Shared_WhatOnePlayerEarnsAnotherCanSpend()
        {
            var wallets = new Wallets(MoneyMode.Shared);

            wallets.For(1).Earn(2_000_000);

            Assert.That(wallets.For(2).TrySpend(1_500_000), Is.True);
            Assert.That(wallets.Hato.Balance, Is.EqualTo(500_000));
        }

        [Test]
        public void Shared_TheHatoStartsWithTheStartingMoney()
        {
            var wallets = new Wallets(MoneyMode.Shared, startingMoney: 750_000);

            Assert.That(wallets.Hato.Balance, Is.EqualTo(750_000));
        }

        [Test]
        public void PerPlayer_EachPlayerHasTheirOwnWallet()
        {
            var wallets = new Wallets(MoneyMode.PerPlayer);

            Assert.That(wallets.For(1), Is.Not.SameAs(wallets.For(2)));
            Assert.That(wallets.For(1), Is.SameAs(wallets.For(1)), "a player keeps the same wallet");
            Assert.That(wallets.For(2).Owner, Is.EqualTo(WalletOwner.Player(2)));
        }

        [Test]
        public void PerPlayer_WhatOnePlayerEarnsDoesNotReachAnother()
        {
            var wallets = new Wallets(MoneyMode.PerPlayer);

            wallets.For(1).Earn(2_000_000);

            Assert.That(wallets.For(1).Balance, Is.EqualTo(2_000_000));
            Assert.That(wallets.For(2).Balance, Is.EqualTo(0));
            Assert.That(wallets.For(2).TrySpend(1), Is.False);
        }

        [Test]
        public void PerPlayer_EveryPlayerStartsWithTheStartingMoney()
        {
            var wallets = new Wallets(MoneyMode.PerPlayer, startingMoney: 100_000);

            Assert.That(wallets.For(1).Balance, Is.EqualTo(100_000));
            Assert.That(wallets.For(5).Balance, Is.EqualTo(100_000));
        }

        [Test]
        public void PerPlayer_HasNoHatoWallet()
        {
            Assert.That(new Wallets(MoneyMode.PerPlayer).Hato, Is.Null);
        }
    }
}
