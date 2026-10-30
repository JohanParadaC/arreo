using System;
using Arreo.Economy;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class CattleSaleTests
    {
        [Test]
        public void Total_IsHeadTimesThePricePerHead()
        {
            Assert.That(CattleSale.Total(head: 7, pricePerHead: 1_200_000), Is.EqualTo(8_400_000));
        }

        [Test]
        public void Total_NoCattle_IsZero()
        {
            Assert.That(CattleSale.Total(0, 1_200_000), Is.EqualTo(0));
        }

        [Test]
        public void Total_ABigSale_DoesNotOverflow()
        {
            // 4 000 million pesos: more than an int holds.
            Assert.That(CattleSale.Total(2000, 2_000_000), Is.EqualTo(4_000_000_000L));
        }

        [Test]
        public void Total_NegativeHeadOrPrice_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CattleSale.Total(-1, 1_000));
            Assert.Throws<ArgumentOutOfRangeException>(() => CattleSale.Total(3, -1_000));
        }

        [Test]
        public void Format_WritesPesosWithDotsBetweenThousands()
        {
            Assert.That(Money.Format(8_400_000), Is.EqualTo("$8.400.000"));
            Assert.That(Money.Format(950), Is.EqualTo("$950"));
            Assert.That(Money.Format(0), Is.EqualTo("$0"));
        }
    }
}
