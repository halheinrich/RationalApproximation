using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// A <see cref="SurvivorSearch"/> whose cost is about <c>log Q</c> plus the number of survivors:
/// intersect the enclosures, bracket the intersection's lower end between two neighbours of the
/// Farey sequence of order <c>Q</c>, then step upward through that sequence until past the upper
/// end.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its order is strictly increasing value</b>, and that is <i>not</i> simplest first. The first
/// survivor is the leftmost rational of denominator at or below the bound in the intersection,
/// which at a large bound usually has a large denominator; a simpler survivor further right comes
/// later. Sorting into <see cref="DenominatorWalk"/>'s order would mean holding every survivor
/// before yielding the first, which gives up both laziness and bounded memory - the two things this
/// type is for. A caller wanting simplest first holds a <see cref="DenominatorWalk"/>.
/// </para>
/// <para>
/// <b>It intersects the enclosures before anything else.</b> A rational lies in every closed
/// interval exactly when it lies in their intersection, so the enclosures collapse to one pair of
/// endpoints - the greatest lower end and the least upper end - and no candidate is ever put to an
/// individual enclosure. Where those cross, nothing survives and nothing is walked; the same is true
/// of a bound of zero, which admits no denominator.
/// </para>
/// <para>
/// <b>The facts it rests on are those of the Farey sequence</b>, the rationals in lowest terms of
/// denominator at or below <c>Q</c> in increasing order. Two consecutive terms <c>a/b &lt; c/d</c>
/// satisfy <c>bc - ad = 1</c>, <c>b, d &lt;= Q</c> and <c>b + d &gt; Q</c>, and the term after
/// <c>c/d</c> is <c>(kc - a)/(kd - b)</c> with <c>k = floor((Q + b)/d)</c>. The sequence is
/// usually stated on <c>[0, 1]</c>; adding an integer to every term maps the rationals of order
/// <c>Q</c> onto themselves and preserves all three facts, so the walk runs on the whole line,
/// across zero and between integers alike. These are standard results rather than this type's
/// inventions, and nothing here relies on them unchecked: the tests hold the bracket to its
/// certificate from the definition, and every result to <see cref="DenominatorWalk"/> and to an
/// independent brute force.
/// </para>
/// <para>
/// <b>Why about <c>log Q</c>.</b> The bracket is a Stern-Brocot descent towards the lower end, with
/// every run of moves in one direction taken as a single batch; a batch consumes one term of the
/// lower end's continued fraction, and the convergents' denominators grow at least as fast as
/// the Fibonacci numbers, so a denominator of <c>Q</c> is passed within about <c>log Q</c> terms.
/// Each step of the walk after it is a fixed number of exact operations and yields one survivor.
/// So precision alone sets how far a search can reach, where <see cref="DenominatorWalk"/> is
/// linear in <c>Q</c>.
/// </para>
/// <para>
/// <b>Every arithmetic step is exact.</b> The floors and ceilings are directed roundings of exact
/// rationals, and every comparison is between <see cref="BigRational"/> values. An endpoint that is
/// itself a rational of order <c>Q</c> is therefore a survivor exactly as the definition says: the
/// intersection is closed at both ends.
/// </para>
/// <para>
/// <b>Its invariants are checked on every batch and step, always, not only in a debug build.</b>
/// Most defects in this walk make it spin rather than answer wrongly: a batch that moves by zero,
/// or a run of batches cut short, loses progress without losing correctness, and a spin reports
/// nothing - in a test it waits out a time budget, and in an app it is a frozen caller. So the
/// descent checks after every batch that it moved, that it alternated sides, that the value stays
/// bracketed, that both denominators lie in <c>[1, Q]</c>, and that the pair stays unimodular; the
/// walk checks after every step that the new term is larger, of denominator in <c>[1, Q]</c>, and
/// consecutive with the last by <c>bc - ad = 1</c> and <c>b + d &gt; Q</c>. Each is computed from
/// the definition and never by re-deriving <c>k</c>, so a check cannot share a defect with the
/// arithmetic it watches. Together they make termination provable - the descent's <c>b + d</c>
/// strictly grows to the bound, the walk's terms strictly grow past the upper end - and a failure
/// throws <see cref="UnreachableException"/>, since it is a defect here and never a fault in the
/// arguments. The cost is a constant per batch or step.
/// </para>
/// <para>
/// <b>Never its own oracle.</b> It shares no enumeration with <see cref="DenominatorWalk"/>, the
/// reference, and every result it gives is checked against that walk. A defect found in this type
/// is fixed here and never by adjusting the reference to agree.
/// </para>
/// </remarks>
public sealed class FareyWalk : SurvivorSearch
{
    /// <summary>The walk, over pre-validated arguments.</summary>
    private protected override IEnumerable<BigRational> Enumerate(
        Approximation[] enclosures,
        BigInteger denominatorBound)
    {
        (BigRational lower, BigRational upper) = Intersect(enclosures);

        if (denominatorBound.IsZero || lower > upper)
        {
            yield break;
        }

        (BigInteger a, BigInteger b, BigInteger c, BigInteger d) = Bracket(lower, denominatorBound);

        // c/d is the least term at or above the lower end, so every term from here to the upper
        // end, inclusive, is a survivor and no other rational is.
        while (new BigRational(c, d) <= upper)
        {
            BigRational yielded = new(c, d);
            yield return yielded;

            BigInteger k = Floor(new BigRational(denominatorBound + b, d));
            (a, b, c, d) = (c, d, (k * c) - a, (k * d) - b);

            // Checked from the definition of consecutive terms of order Q, never by re-deriving k.
            // Strictly increasing terms of bounded denominator are finitely many below the upper
            // end, so these are what make the loop provably end; b + d > Q is what says no term
            // was skipped, so no survivor was.
            Require(d.Sign > 0 && d <= denominatorBound, "the next term's denominator lies in [1, Q]", a, b, c, d);
            Require(new BigRational(c, d) > yielded, "each term exceeds the one before it", a, b, c, d);
            Require((b * c) - (a * d) == BigInteger.One, "consecutive terms satisfy bc - ad = 1", a, b, c, d);
            Require(b + d > denominatorBound, "consecutive terms satisfy b + d > Q", a, b, c, d);
        }
    }

    /// <summary>
    /// The two consecutive terms of the Farey sequence of order <paramref name="denominatorBound"/>
    /// that straddle <paramref name="lower"/>: <c>a/b &lt; lower &lt;= c/d</c>.
    /// </summary>
    /// <param name="lower">The value to bracket; any sign, and itself possibly a term.</param>
    /// <param name="denominatorBound">The order of the sequence. At least one.</param>
    /// <returns>
    /// The two terms as the unreduced integers the walk continues from, satisfying
    /// <c>a/b &lt; lower &lt;= c/d</c>, <c>bc - ad = 1</c>, <c>b, d &lt;= Q</c> and
    /// <c>b + d &gt; Q</c> - which together say they are consecutive terms of order <c>Q</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominatorBound"/> is below one.</exception>
    /// <exception cref="UnreachableException">The descent broke one of its own invariants - a defect in this type.</exception>
    /// <remarks>
    /// <para>
    /// <c>internal</c> for the tests, which check the certificate above from the definition and
    /// never through the recurrence that consumes it - the seam
    /// <see cref="HeightSweep.BestDenominator"/> set the precedent for. Through
    /// <see cref="SurvivorSearch.Survivors"/> a bracket that was merely close would show only as a
    /// survivor missing or extra at the lower end.
    /// </para>
    /// <para>
    /// The descent starts from the integers either side of the value, <c>(n - 1)/1</c> and
    /// <c>n/1</c> with <c>n</c> its ceiling, which are consecutive terms of order 1. While a mediant
    /// of the two would still be of order <c>Q</c>, it replaces the end on its own side of the
    /// value - the Stern-Brocot descent - except that a run of moves on one side is taken at once,
    /// as many as keep the invariant and the order. The value on the right end counts as that end's
    /// side, which is what makes the right end the least term at or above it.
    /// </para>
    /// </remarks>
    internal static (BigInteger A, BigInteger B, BigInteger C, BigInteger D) Bracket(
        BigRational lower,
        BigInteger denominatorBound)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(denominatorBound, BigInteger.One);

        BigInteger n = Ceiling(lower);
        BigInteger a = n - BigInteger.One;
        BigInteger b = BigInteger.One;
        BigInteger c = n;
        BigInteger d = BigInteger.One;
        RequireBracket(lower, denominatorBound, a, b, c, d);

        // Which end the previous batch moved: -1 left, +1 right, 0 before the first.
        int previousSide = 0;

        while (b + d <= denominatorBound)
        {
            int side;

            if (new BigRational(a + c, b + d) < lower)
            {
                side = -1;

                // Move the left end to (a + kc)/(b + kd) for the largest k keeping it below the
                // value and its denominator within the bound. Below the value means
                // k(c - lower*d) < lower*b - a; the mediant being below makes k = 1 valid. When
                // the right end IS the value the left side is never violated, and only the bound
                // limits k.
                BigInteger k = Floor(new BigRational(denominatorBound - b, d));
                BigRational rightGap = c - (lower * d);

                if (!rightGap.IsZero)
                {
                    k = BigInteger.Min(k, Ceiling(((lower * b) - a) / rightGap) - BigInteger.One);
                }

                Require(k.Sign > 0, "every batch of the descent moves by at least one", a, b, c, d);
                a += k * c;
                b += k * d;
            }
            else
            {
                side = +1;

                // Move the right end to (ka + c)/(kb + d) for the largest k keeping it at or above
                // the value and its denominator within the bound: k(lower*b - a) <= c - lower*d,
                // where lower*b - a is positive because the left end is strictly below.
                BigInteger k = BigInteger.Min(
                    Floor(new BigRational(denominatorBound - d, b)),
                    Floor((c - (lower * d)) / ((lower * b) - a)));

                Require(k.Sign > 0, "every batch of the descent moves by at least one", a, b, c, d);
                c += k * a;
                d += k * b;
            }

            // A maximal batch leaves the next mediant on the other side of the value, so batches
            // alternate; one that did not would be a batch cut short, and a run of them is how a
            // logarithmic descent turns linear.
            Require(side != previousSide, "batches of the descent alternate sides", a, b, c, d);
            previousSide = side;
            RequireBracket(lower, denominatorBound, a, b, c, d);
        }

        return (a, b, c, d);
    }

    /// <summary>
    /// The descent's invariant after every batch, from the definition: the value is bracketed,
    /// <c>a/b &lt; lower &lt;= c/d</c>; both denominators lie in <c>[1, Q]</c>; and the pair is
    /// unimodular, <c>bc - ad = 1</c>.
    /// </summary>
    private static void RequireBracket(
        BigRational lower,
        BigInteger denominatorBound,
        BigInteger a,
        BigInteger b,
        BigInteger c,
        BigInteger d)
    {
        Require(
            b.Sign > 0 && d.Sign > 0 && b <= denominatorBound && d <= denominatorBound,
            "the descent's denominators lie in [1, Q]",
            a, b, c, d);
        Require(
            new BigRational(a, b) < lower && lower <= new BigRational(c, d),
            "the descent keeps a/b < lower <= c/d",
            a, b, c, d);
        Require((b * c) - (a * d) == BigInteger.One, "the descent keeps bc - ad = 1", a, b, c, d);
    }

    /// <summary>
    /// Throws when one of this type's own invariants fails. A failure is a defect in
    /// <see cref="FareyWalk"/>, never a fault in the caller's arguments, which
    /// <see cref="SurvivorSearch.Survivors"/> has already validated.
    /// </summary>
    /// <remarks>
    /// Always on, not a debug assertion: most defects in this walk make it spin rather than answer
    /// wrongly, and a spin in a released build is a frozen caller with nothing to report. The
    /// message is built only on failure, so a check that holds costs its comparison and nothing
    /// more.
    /// </remarks>
    /// <exception cref="UnreachableException"><paramref name="holds"/> is <see langword="false"/>.</exception>
    private static void Require(bool holds, string invariant, BigInteger a, BigInteger b, BigInteger c, BigInteger d)
    {
        if (!holds)
        {
            throw new UnreachableException(string.Create(
                CultureInfo.InvariantCulture,
                $"FareyWalk broke its own invariant \"{invariant}\" at a/b = {a}/{b}, c/d = {c}/{d}. This is a defect in FareyWalk, not in the arguments."));
        }
    }

    /// <summary>
    /// The greatest lower end and the least upper end, which may cross; where they do, no rational
    /// lies in every enclosure.
    /// </summary>
    private static (BigRational Lower, BigRational Upper) Intersect(Approximation[] enclosures)
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

        return (lower, upper);
    }

    /// <summary>The greatest integer at or below the value, as a directed rounding.</summary>
    private static BigInteger Floor(BigRational value) =>
        BigRational.Round(value, MidpointRounding.ToNegativeInfinity);

    /// <summary>The least integer at or above the value, as a directed rounding.</summary>
    private static BigInteger Ceiling(BigRational value) =>
        BigRational.Round(value, MidpointRounding.ToPositiveInfinity);
}
