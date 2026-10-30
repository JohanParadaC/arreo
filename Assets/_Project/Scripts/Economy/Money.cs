using System.Globalization;

namespace Arreo.Economy
{
    public static class Money
    {
        /// <summary>Pesos the Colombian way: $1.200.000.</summary>
        public static string Format(long pesos) =>
            "$" + pesos.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
