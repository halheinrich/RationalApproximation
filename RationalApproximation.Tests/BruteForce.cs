using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Independent oracles for the search, built from the definitions in the spec rather than from
/// the mechanism under test. A test that reuses the sweep to check the sweep proves only that it
/// agrees with itself.
/// </summary>
/// <remarks>
/// Exact integer and rational arithmetic throughout. Nothing here calls
/// <see cref="DenominatorSweep"/>, and nothing here uses the nearest-rounding the sweep depends
/// on: enclosure membership is decided by <see cref="Approximation.Contains"/>, and the integers
/// in an interval are found with a directed ceiling and floor, which is what "p/q lies in
/// [lo, hi]" means.
/// </remarks>
internal static class BruteForce
{
    /// <summary>
    /// Finds the least height at which some reduced rational is inside the enclosure, by
    /// enumerating rationals in increasing height.
    /// </summary>
    /// <returns>The least such height, or null if none was found within the bound.</returns>
    public static BigInteger? LeastEnclosedHeight(Approximation enclosure, int heightBound)
    {
        for (int height = 1; height <= heightBound; height++)
        {
            if (EnclosedOfHeight(enclosure, height).Count > 0)
            {
                return height;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds every reduced rational of exactly the given naive height that the enclosure contains.
    /// </summary>
    /// <remarks>
    /// A reduced <c>p/q</c> has <c>max(|p|, q) == height</c> exactly when <c>|p| == height</c> or
    /// <c>q == height</c>, so only those two families need enumerating rather than the whole
    /// square.
    /// </remarks>
    public static List<BigRational> EnclosedOfHeight(Approximation enclosure, int height)
    {
        var found = new SortedSet<BigRational>(RationalOrder.Instance);

        for (int q = 1; q <= height; q++)
        {
            Consider(enclosure, height, q, found);
            Consider(enclosure, -height, q, found);
        }

        for (int p = -height; p <= height; p++)
        {
            Consider(enclosure, p, height, found);
        }

        return [.. found];
    }

    /// <summary>
    /// Finds the least denominator at which some rational is inside the enclosure.
    /// </summary>
    /// <remarks>
    /// This is the denominator bound the sweep is really claiming, and § 1 calls it the stronger
    /// statement: it quantifies over every numerator rather than over a height. A rational
    /// <c>p/q</c> lies in <c>[lo, hi]</c> exactly when the integer <c>p</c> lies in
    /// <c>[lo*q, hi*q]</c>, so the question is whether that interval holds an integer at all -
    /// decided here by a ceiling and a floor, with no reference to which one is nearest.
    /// </remarks>
    /// <returns>The least such denominator, or null if none was found within the bound.</returns>
    public static BigInteger? LeastEnclosedDenominator(Approximation enclosure, int denominatorBound)
    {
        for (int q = 1; q <= denominatorBound; q++)
        {
            BigInteger smallest = BigRational.Round(enclosure.Lower * q, MidpointRounding.ToPositiveInfinity);
            BigInteger largest = BigRational.Round(enclosure.Upper * q, MidpointRounding.ToNegativeInfinity);

            if (smallest <= largest)
            {
                return q;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether the given rational is the closest one of its own denominator to the
    /// target, checked against its two neighbours rather than by rounding.
    /// </summary>
    public static bool IsNearestOfItsDenominator(BigRational candidate, BigRational target)
    {
        BigInteger p = candidate.Numerator;
        BigInteger q = candidate.Denominator;
        BigRational distance = BigRational.Abs(candidate - target);

        BigRational below = BigRational.Abs(new BigRational(p - BigInteger.One, q) - target);
        BigRational above = BigRational.Abs(new BigRational(p + BigInteger.One, q) - target);

        return distance <= below && distance <= above;
    }

    private static void Consider(Approximation enclosure, int numerator, int denominator, SortedSet<BigRational> found)
    {
        if (BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator) != BigInteger.One)
        {
            return;
        }

        var value = new BigRational(numerator, denominator);
        if (enclosure.Contains(value))
        {
            found.Add(value);
        }
    }

    private sealed class RationalOrder : IComparer<BigRational>
    {
        public static RationalOrder Instance { get; } = new RationalOrder();

        public int Compare(BigRational x, BigRational y) => x.CompareTo(y);
    }
}
