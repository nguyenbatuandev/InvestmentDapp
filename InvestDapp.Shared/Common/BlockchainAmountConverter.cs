using System;
using System.Globalization;
using System.Numerics;

namespace InvestDapp.Shared.Common
{
    public static class BlockchainAmountConverter
    {
    public const decimal WeiPerBnb = 1_000_000_000_000_000_000m;
    private static readonly BigInteger WeiPerBnbBigInteger = BigInteger.Parse("1000000000000000000");
    private static readonly BigInteger WeiDetectionThreshold = BigInteger.Parse("1000000000000"); // 1e12 wei = 0.000001 BNB

    private const int DefaultLargeValueFractionDigits = 4;
    private const int DefaultMaxFractionDigits = 18;

        public static decimal ToBnb(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0m;
            }

            var trimmed = value.Trim();

            const NumberStyles decimalStyles = NumberStyles.Float | NumberStyles.AllowThousands;

            if (ContainsDecimalSeparator(trimmed))
            {
                if (decimal.TryParse(trimmed, decimalStyles, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    return decimalValue;
                }

                if (decimal.TryParse(trimmed, decimalStyles, CultureInfo.CurrentCulture, out var decimalValueCurrent))
                {
                    return decimalValueCurrent;
                }
            }

            if (BigInteger.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
            {
                if (IsLikelyWei(integerValue))
                {
                    return ConvertWeiToBnb(integerValue);
                }

                return (decimal)integerValue;
            }

            if (decimal.TryParse(trimmed, decimalStyles, CultureInfo.InvariantCulture, out var fallbackInvariant))
            {
                return fallbackInvariant;
            }

            if (decimal.TryParse(trimmed, decimalStyles, CultureInfo.CurrentCulture, out var fallbackCurrent))
            {
                return fallbackCurrent;
            }

            var normalized = trimmed.Replace(',', '.');
            if (decimal.TryParse(normalized, decimalStyles, CultureInfo.InvariantCulture, out var fallbackNormalized))
            {
                return fallbackNormalized;
            }

            return 0m;
        }

        public static string FromBnb(decimal amountInBnb)
        {
            var weiDecimal = decimal.Multiply(amountInBnb, WeiPerBnb);
            var roundedWei = decimal.Round(weiDecimal, 0, MidpointRounding.AwayFromZero);
            var weiBigInteger = new BigInteger(roundedWei);
            return weiBigInteger.ToString();
        }

        public static string FromBnb(double amountInBnb)
        {
            return FromBnb((decimal)amountInBnb);
        }

        public static string FormatBnb(decimal amount, int largeValueFractionDigits = DefaultLargeValueFractionDigits, int maxFractionDigits = DefaultMaxFractionDigits)
        {
            if (largeValueFractionDigits < 0)
            {
                largeValueFractionDigits = DefaultLargeValueFractionDigits;
            }

            if (maxFractionDigits <= 0 || maxFractionDigits > 28)
            {
                maxFractionDigits = DefaultMaxFractionDigits;
            }

            if (amount == 0)
            {
                return "0";
            }

            var abs = Math.Abs(amount);

            if (abs >= 1m)
            {
                return amount.ToString($"N{largeValueFractionDigits}", CultureInfo.InvariantCulture);
            }

            var fractionFormat = "0." + new string('#', maxFractionDigits);
            var formatted = amount.ToString(fractionFormat, CultureInfo.InvariantCulture);

            if (formatted == "0." || formatted == "-0.")
            {
                formatted = amount.ToString("0.############################", CultureInfo.InvariantCulture);
            }

            return formatted.TrimEnd('0').TrimEnd('.');
        }

        private static bool ContainsDecimalSeparator(string value)
        {
            return value.Contains('.') || value.Contains(',') || value.Contains('e') || value.Contains('E');
        }

        private static bool IsLikelyWei(BigInteger value)
        {
            if (value == BigInteger.Zero)
            {
                return false;
            }

            if (value < BigInteger.Zero)
            {
                value = BigInteger.Negate(value);
            }

            return value >= WeiDetectionThreshold;
        }

        private static decimal ConvertWeiToBnb(BigInteger weiValue)
        {
            var quotient = BigInteger.DivRem(weiValue, WeiPerBnbBigInteger, out var remainder);
            decimal result = (decimal)quotient;

            if (remainder == BigInteger.Zero)
            {
                return result;
            }

            var fractional = (decimal)remainder / WeiPerBnb;
            return result + fractional;
        }
    }
}
