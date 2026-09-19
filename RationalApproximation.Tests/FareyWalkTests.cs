using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="FareyWalk"/>: the contract, inherited; its own order; its cost and laziness at a bound
/// no linear walk could reach; its bracket, held to a certificate from the definition; and every
/// answer over a generated family held to two oracles.
/// </summary>
/// <remarks>
/// <b>This walk is never its own oracle.</b> <see cref="BruteForce.SurvivorsByIntersection"/>
/// shares its first step, the intersection, and not its enumeration; <see cref="DenominatorWalk"/>
/// shares neither. Agreement with both is the evidence, and a disagreement is fixed in this walk.
/// </remarks>
public sealed class FareyWalkTests : SurvivorSearchContractTests
{
    private static readonly BigInteger FarBound = BigInteger.Pow(10, 30);

    private static readonly BigRational TinyHalfWidth = new(BigInteger.One, BigInteger.Pow(10, 60));

    /// <summary>
    /// A 36-digit rational with a long continued fraction, so its bracket takes many batches. Its
    /// digits are those of a well-known constant, chosen for the shape of the expansion and not
    /// because the value is interesting.
    /// </summary>
    private static readonly BigRational ThirtySixDigits = new(
        BigInteger.Parse("314159265358979323846264338327950288", System.Globalization.CultureInfo.InvariantCulture),
        BigInteger.Pow(10, 35));

    /// <inheritdoc/>
    protected override SurvivorSearch Search { get; } = new FareyWalk();

    // ---------- its order ----------

    [Fact]
    public void Survivors_AreYieldedInStrictlyIncreasingValue()
    {
        int ordered = 0;

        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            IReadOnlyList<BigRational> found = Run(enclosures, bound);

            for (int i = 1; i < found.Count; i++)
            {
                Assert.True(found[i - 1] < found[i], Inv($"{found[i]} follows {found[i - 1]} without increasing."));
            }

            ordered += found.Count;
        }

        // Fixtures that were all empty or all single would order trivially and prove nothing.
        Assert.True(ordered > 100, Inv($"Only {ordered} survivors were ordered."));
    }

    [Fact]
    public void Survivors_ComeLeftToRight_WhichIsNotSimplestFirst()
    {
        // The difference from DenominatorWalk, which yields 6 first here. A caller reading the
        // first survivor as the simplest is reading the wrong walk.
        Assert.Equal(new[] { Ratio(59, 10), Ratio(6, 1), Ratio(61, 10) }, Run([Enclosure(6, 1, 1, 10)], 10));
    }

    // ---------- two oracles over a generated family ----------

    /// <summary>
    /// Enclosures centred on small rationals and integers, of radius zero, a seventh, a half, one and
    /// three halves, so their endpoints are small rationals, integers or both - and often terms of
    /// the Farey sequence at the bounds used, which is where a strict comparison would part from an
    /// inclusive one.
    /// </summary>
    private static Approximation[] GeneratedEnclosures()
    {
        BigRational[] centres = [Ratio(-7, 3), Ratio(-1, 1), Ratio(-1, 2), BigRational.Zero, Ratio(1, 3), Ratio(1, 1), Ratio(22, 7), Ratio(6, 1)];
        BigRational[] radii = [BigRational.Zero, Ratio(1, 7), Ratio(1, 2), Ratio(1, 1), Ratio(3, 2)];

        return [.. centres.SelectMany(centre => radii.Select(radius => Approximation.Create(centre, radius)))];
    }

    /// <summary>
    /// One, two and three enclosures drawn from <see cref="GeneratedEnclosures"/>: each alone, pairs
    /// at three strides - near neighbours that overlap and far ones that do not - and triples.
    /// </summary>
    private static List<Approximation[]> GeneratedSets()
    {
        Approximation[] all = GeneratedEnclosures();
        var sets = new List<Approximation[]>();

        for (int i = 0; i < all.Length; i++)
        {
            sets.Add([all[i]]);

            foreach (int stride in new[] { 1, 5, 13 })
            {
                if (i + stride < all.Length)
                {
                    sets.Add([all[i], all[i + stride]]);
                }
            }

            if (i + 7 < all.Length)
            {
                sets.Add([all[i], all[i + 7], all[i + 3]]);
            }
        }

        return sets;
    }

    [Fact]
    public void Survivors_AgreeWithTheIntersectionOracleAndTheReference_OverAGeneratedFamily()
    {
        var reference = new DenominatorWalk();
        int compared = 0;
        int nonEmpty = 0;
        int integerEnds = 0;
        int zeroWidth = 0;
        int negative = 0;
        int straddling = 0;
        int disjoint = 0;
        var sizes = new HashSet<int>();

        foreach (Approximation[] enclosures in GeneratedSets())
        {
            BigRational lower = enclosures.Max(enclosure => enclosure.Lower);
            BigRational upper = enclosures.Min(enclosure => enclosure.Upper);

            foreach (int bound in new[] { 1, 2, 7, 30 })
            {
                IReadOnlyList<BigRational> found = Run(enclosures, bound);

                // Its own order is value order, which is the brute force's order too, so this
                // compares the sequence and not just the set.
                Assert.Equal(BruteForce.SurvivorsByIntersection(enclosures, bound), found);

                List<BigRational> byReference = BoundedSearch.CompleteWithin(
                    () => new List<BigRational>(reference.Survivors(enclosures, bound)),
                    "The reference walk");
                byReference.Sort();
                Assert.Equal(byReference, found);

                compared++;
                nonEmpty += found.Count > 0 ? 1 : 0;
            }

            // What the family was built to exhibit, counted so that a narrower family fails here
            // rather than passing on shapes it no longer holds.
            integerEnds += lower.Denominator.IsOne && upper.Denominator.IsOne ? 1 : 0;
            zeroWidth += lower == upper ? 1 : 0;
            negative += upper.Sign < 0 ? 1 : 0;
            straddling += lower.Sign < 0 && upper.Sign > 0 ? 1 : 0;
            disjoint += lower > upper ? 1 : 0;
            sizes.Add(enclosures.Length);
        }

        Assert.True(compared > 500, Inv($"Only {compared} cases were compared."));
        Assert.True(nonEmpty > 100, Inv($"Only {nonEmpty} cases had survivors."));
        Assert.True(
            integerEnds > 0 && zeroWidth > 0 && negative > 0 && straddling > 0 && disjoint > 0,
            Inv($"Shapes: {integerEnds} integer-ended, {zeroWidth} zero-width, {negative} negative, ") +
            Inv($"{straddling} straddling zero, {disjoint} disjoint - one is unexercised."));
        Assert.True(sizes.SetEquals(Enumerable.Range(1, 3)), Inv($"Set sizes seen: {string.Join(", ", sizes)}."));
    }

    // ---------- cost and laziness, at a bound no linear walk could reach ----------

    [Fact]
    public void Survivors_AtABoundOf10To30_OnAHalfWidthOf1eMinus60_FinishInsideTheBudget()
    {
        // The cost guard: a linear walk to 10^30 would take about 10^30 steps, so this reddens at
        // once if the walk ever stops being logarithmic, and no amount of load makes it flaky.
        // Where the isolation rule proves the answer it is asserted exactly; elsewhere the walk
        // must finish and every survivor must be one.
        BigRational[] centres =
        [
            Ratio(6, 1),
            Ratio(-22, 7),
            Ratio(355, 113) + new BigRational(BigInteger.One, BigInteger.Pow(10, 40)),
            ThirtySixDigits,
        ];

        foreach (BigRational centre in centres)
        {
            Approximation enclosure = Approximation.Create(centre, TinyHalfWidth);
            IReadOnlyList<BigRational> found = Run([enclosure], FarBound);

            Assert.All(found, survivor =>
            {
                Assert.True(enclosure.Contains(survivor));
                Assert.True(survivor.Denominator <= FarBound);
            });

            if (SurvivorSearch.IsReachable(centre, FarBound) && SurvivorSearch.IsIsolated(centre, FarBound, TinyHalfWidth))
            {
                Assert.Equal(new[] { centre }, found);
            }
        }
    }

    [Fact]
    public void Survivors_CanBeTakenAFewAtATime_FromAWideEnclosureAtABoundOf10To30()
    {
        // 6 +/- 1/10 holds on the order of 10^59 rationals of denominator at or below 10^30, so
        // this returns at once only if they are walked one at a time. The first is the lower end
        // itself, 59/10, a term of every order from 10 up.
        List<BigRational> first = BoundedSearch.CompleteWithin(
            () => new List<BigRational>(Search.Survivors([Enclosure(6, 1, 1, 10)], FarBound).Take(3)),
            "A truncated survivor search");

        Assert.Equal(3, first.Count);
        Assert.Equal(Ratio(59, 10), first[0]);
        Assert.True(first[0] < first[1] && first[1] < first[2]);
        Assert.All(first, survivor => Assert.True(survivor.Denominator <= FarBound));
    }

    // ---------- the bracket, checked from the definition ----------

    /// <summary>Values to bracket: integers, terms, near-terms, negatives, zero, and irrational-looking ones.</summary>
    private static BigRational[] BracketedValues() =>
    [
        Ratio(-7, 3),
        Ratio(-1, 1),
        BigRational.Zero,
        Ratio(1, 2),
        Ratio(1, 3) + new BigRational(BigInteger.One, BigInteger.Pow(10, 50)),
        Ratio(355, 113),
        Ratio(6, 1) - new BigRational(BigInteger.One, BigInteger.Pow(10, 40)),
        Ratio(-22, 7) + new BigRational(BigInteger.One, BigInteger.Pow(10, 45)),
        ThirtySixDigits,
    ];

    /// <summary>
    /// Brackets under the shared budget. A descent that stops making progress spins rather than
    /// returning a wrong answer, so an unbounded call would hang the run instead of failing it.
    /// </summary>
    private static (BigInteger A, BigInteger B, BigInteger C, BigInteger D) BoundedBracket(BigRational value, BigInteger bound) =>
        BoundedSearch.CompleteWithin(
            () => new[] { FareyWalk.Bracket(value, bound) },
            Inv($"Bracketing {value} at Q = {bound}"))[0];

    [Fact]
    public void Bracket_IsTwoConsecutiveTermsStraddlingTheValue_ByTheCertificate()
    {
        // Checked from the definition and never through the recurrence that consumes it:
        // a/b < value <= c/d, bc - ad = 1, b and d within the bound, and b + d beyond it. Together
        // those say no rational of denominator at or below the bound lies strictly between.
        int checkedBrackets = 0;

        foreach (BigRational value in BracketedValues())
        {
            foreach (BigInteger bound in new BigInteger[] { 1, 2, 7, 113, 1000, FarBound })
            {
                (BigInteger a, BigInteger b, BigInteger c, BigInteger d) = BoundedBracket(value, bound);
                string where = Inv($"{value} at Q = {bound}: {a}/{b}, {c}/{d}");

                Assert.True(b.Sign > 0 && d.Sign > 0, where);
                Assert.True(new BigRational(a, b) < value, where);
                Assert.True(value <= new BigRational(c, d), where);
                Assert.True((b * c) - (a * d) == BigInteger.One, where);
                Assert.True(b <= bound && d <= bound, where);
                Assert.True(b + d > bound, where);

                checkedBrackets++;
            }
        }

        Assert.Equal(BracketedValues().Length * 6, checkedBrackets);
    }

    [Fact]
    public void Bracket_OfATerm_PutsTheTermOnTheRight()
    {
        // The value counts as the right end's side, which is what makes the walk's first
        // survivor the lower end itself when that end is a term - the inclusive lower bound.
        (_, _, BigInteger c, BigInteger d) = BoundedBracket(Ratio(355, 113), 113);
        Assert.Equal(new BigRational(355, 113), new BigRational(c, d));

        (BigInteger a, BigInteger b, c, d) = BoundedBracket(Ratio(6, 1), 1);
        Assert.Equal((new BigInteger(5), BigInteger.One, new BigInteger(6), BigInteger.One), (a, b, c, d));
    }

    [Fact]
    public void Bracket_BelowOrderOne_IsRefused()
    {
        // Order zero holds no terms, so there is nothing to bracket with; the walk never asks.
        Assert.Throws<ArgumentOutOfRangeException>(() => FareyWalk.Bracket(Ratio(1, 2), BigInteger.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => FareyWalk.Bracket(Ratio(1, 2), BigInteger.MinusOne));
    }
}
