using System;
using Arreo.Economy;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class WalletTests
    {
        [Test]
        public void NewWallet_StartsWithTheGivenMoney()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 500);

            Assert.That(wallet.Balance, Is.EqualTo(500));
        }

        [Test]
        public void Earn_AddsToTheBalance()
        {
            var wallet = new Wallet(WalletOwner.Hato);

            wallet.Earn(1_200_000);
            wallet.Earn(300_000);

            Assert.That(wallet.Balance, Is.EqualTo(1_500_000));
        }

        [Test]
        public void TrySpend_WithEnoughMoney_TakesItAndSucceeds()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 1000);

            Assert.That(wallet.TrySpend(400), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(600));
        }

        [Test]
        public void TrySpend_ExactlyTheBalance_LeavesZero()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 1000);

            Assert.That(wallet.TrySpend(1000), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(0));
        }

        [Test]
        public void TrySpend_MoreThanTheBalance_FailsAndLeavesItUntouched()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 1000);

            Assert.That(wallet.TrySpend(1001), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(1000), "a wallet never goes negative");
        }

        [Test]
        public void CanAfford_ComparesWithTheBalance()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 1000);

            Assert.That(wallet.CanAfford(1000), Is.True);
            Assert.That(wallet.CanAfford(1001), Is.False);
        }

        [Test]
        public void NegativeAmounts_AreRejected()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 1000);

            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Earn(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(WalletOwner.Hato, balance: -5));
            Assert.That(wallet.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void Changed_IsRaisedOnlyWhenTheBalanceMoves()
        {
            var wallet = new Wallet(WalletOwner.Hato, balance: 100);
            int changes = 0;
            wallet.Changed += _ => changes++;

            wallet.Earn(50);
            wallet.TrySpend(500);
            wallet.TrySpend(30);
            wallet.Earn(0);

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void Owner_TellsTheHatoFromAPlayer()
        {
            WalletOwner player = WalletOwner.Player(3);

            Assert.That(WalletOwner.Hato.IsHato, Is.True);
            Assert.That(player.IsHato, Is.False);
            Assert.That(player.PlayerId, Is.EqualTo(3));
            Assert.That(player, Is.EqualTo(WalletOwner.Player(3)));
            Assert.That(player, Is.Not.EqualTo(WalletOwner.Player(4)));
            Assert.That(player, Is.Not.EqualTo(WalletOwner.Hato));
        }
    }
}
