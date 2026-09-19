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
    /// This is the denominator bound <see cref="DenominatorSweep"/> is really claiming, and § 1
    /// calls it the stronger statement: it quantifies over every numerator rather than over a
    /// height. <see cref="HeightSweep"/> above one claims a height bound instead, so the axis is
    /// named here rather than left to "the sweep". A rational
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
    /// Finds every rational of denominator at or below the bound that lies in all of the given
    /// enclosures, by intersecting the intervals first and enumerating afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The oracle for every <see cref="SurvivorSearch"/>. Against <see cref="DenominatorWalk"/> it
    /// is independent in both steps that could go wrong. It never asks which enclosure is
    /// narrowest - a rational lies in every closed interval exactly when it lies in their
    /// intersection, so the enclosures collapse to one pair of endpoints before any candidate
    /// exists, and no candidate is ever put to an individual enclosure. And it does no reduction
    /// test: every pair in range is handed to <see cref="BigRational"/>, whose own lowest-terms
    /// invariant is what collapses <c>12/2</c> onto <c>6/1</c>, so a set built this way cannot
    /// inherit a defect in that walk's greatest-common-divisor skip.
    /// </para>
    /// <para>
    /// Against <see cref="FareyWalk"/> it is independent in one step only. That walk intersects
    /// first too, and shares nothing after it: it enumerates by the Farey recurrence where this
    /// enumerates every numerator of every denominator. So a defect in the intersection could be
    /// mirrored here, which is why that walk is also held to <see cref="DenominatorWalk"/>, which
    /// shares neither step.
    /// </para>
    /// <para>
    /// The result is ordered by value. That is <b>not</b> <see cref="DenominatorWalk"/>'s order, so a
    /// test comparing the reference against this cannot accidentally pass on order alone. It
    /// <i>is</i> <see cref="FareyWalk"/>'s order, so a comparison with that walk checks its order
    /// along with its answer - and a comparison that sorts first cannot tell that walk's order
    /// right from wrong, which is why its order has its own test.
    /// </para>
    /// </remarks>
    public static List<BigRational> SurvivorsByIntersection(
        IReadOnlyList<Approximation> enclosures,
        int denominatorBound)
    {
        BigRational lower = enclosures[0].Lower;
        BigRational upper = enclosures[0].Upper;

        foreach (Approximation enclosure in enclosures)
        {
            if (enclosure.Lower > lower)
            {
                lower = enclosure.Lower;
            }

            if (enclosure.Upper < upper)
            {
                upper = enclosure.Upper;
            }
        }

        var found = new SortedSet<BigRational>(RationalOrder.Instance);

        for (int q = 1; q <= denominatorBound; q++)
        {
            BigInteger smallest = BigRational.Round(lower * q, MidpointRounding.ToPositiveInfinity);
            BigInteger largest = BigRational.Round(upper * q, MidpointRounding.ToNegativeInfinity);

            for (BigInteger p = smallest; p <= largest; p++)
            {
                found.Add(new BigRational(p, q));
            }
        }

        return [.. found];
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

    /// <summary>
    /// Determines whether the given rational is the closest one of its own numerator to the
    /// target, checked against its two neighbours rather than by rounding.
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="IsNearestOfItsDenominator"/>, and it cannot be written as a
    /// rounding of <c>p/x</c>: <c>p/q</c> is a hyperbola in <c>q</c>, so the denominator nearest
    /// <c>p/x</c> is not always the one minimising the distance. Comparing against both integer
    /// neighbours is the definition, and is what <see cref="HeightSweep"/> is checked against.
    /// </remarks>
    public static bool IsNearestOfItsNumerator(BigRational candidate, BigRational target)
    {
        BigInteger p = candidate.Numerator;
        BigInteger q = candidate.Denominator;
        BigRational distance = BigRational.Abs(candidate - target);

        BigRational above = BigRational.Abs(new BigRational(p, q + BigInteger.One) - target);

        if (q == BigInteger.One)
        {
            return distance <= above;
        }

        BigRational below = BigRational.Abs(new BigRational(p, q - BigInteger.One) - target);
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
