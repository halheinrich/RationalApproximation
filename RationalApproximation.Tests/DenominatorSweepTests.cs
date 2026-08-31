using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="DenominatorSweep"/>, the reference searcher. The claims that matter are checked
/// against <see cref="BruteForce"/>, which is built from the definitions rather than from the
/// sweep: a test that reuses the mechanism proves only self-consistency.
/// </summary>
public class DenominatorSweepTests
{
    /// <summary>
    /// Enclosures spanning the shapes that change the search: above one and below one, since the
    /// height is carried by the numerator in the first case and the denominator in the second;
    /// negative; exact; straddling zero; and a tie at the first denominator.
    /// </summary>
    private static Approximation[] Targets() =>
    [
        Approximation.Create(Ratio(22, 7), Ratio(1, 100)),
        Approximation.Create(Ratio(17, 50), Ratio(1, 1000)),
        Approximation.Create(Ratio(-7, 3), Ratio(1, 4)),
        Approximation.Create(Ratio(1, 10), Ratio(1, 1)),
        Approximation.Create(Ratio(1, 3), Ratio(1, 1000)),
        Approximation.Create(Ratio(3, 2), Ratio(1, 100)),
        Approximation.Create(Ratio(-3, 2), Ratio(1, 100)),
        Approximation.Create(Ratio(355, 113), Ratio(1, 100000)),
        Approximation.Exact(Ratio(5, 13)),
        Approximation.Exact(BigRational.Zero),
        Approximation.Exact(Ratio(-8, 5)),
    ];

    private const int CandidateCap = 5000;
    private static readonly TimeSpan SearchBudget = TimeSpan.FromSeconds(10);
    private static volatile bool searchOverran;

    /// <summary>
    /// Runs a search to completion, bounded twice.
    /// </summary>
    /// <remarks>
    /// A defective search fails in two ways that inspecting its output cannot catch, because
    /// there is no output to inspect: it can yield improvements forever, or spin without yielding
    /// at all. Either way an unbounded test <i>hangs</i>, and a hanging test does not fail - it
    /// reports nothing and burns the CI job's whole timeout, which is the same
    /// silence-reads-as-success failure this project exists to avoid. The count cap catches the
    /// first mode deterministically; the time budget catches the second, and latches so that one
    /// abandoned thread is the worst case rather than one per assertion.
    /// </remarks>
    private static List<RationalCandidate> Run(Approximation enclosure)
    {
        Assert.False(searchOverran, "An earlier search overran its budget; not starting another.");

        List<RationalCandidate>? candidates = null;
        Task task = Task.Run(() => candidates = [.. new DenominatorSweep().Search(enclosure).Take(CandidateCap)]);

        if (!task.Wait(SearchBudget))
        {
            searchOverran = true;
            Assert.Fail(Inv($"The search did not finish within {SearchBudget.TotalSeconds} seconds for {enclosure.Value}."));
        }

        Assert.NotNull(candidates);
        Assert.True(
            candidates!.Count < CandidateCap,
            Inv($"The search yielded {CandidateCap} candidates without terminating for {enclosure.Value}."));

        return candidates!;
    }

    // ---------- the least-height claim, against an independent oracle ----------

    [Fact]
    public void Search_TerminatesAtTheLeastHeightEnclosedRational_WhenTheEnclosureIsNarrow()
    {
        // The claim of section 3. It holds whenever the enclosure contains at most one integer,
        // which every realistic enclosure does; the exception is pinned by its own test below.
        foreach (Approximation enclosure in Targets())
        {
            if (EnclosedIntegerCount(enclosure) >= 2)
            {
                continue;
            }

            RationalCandidate terminal = Run(enclosure)[^1];
            BigInteger? leastHeight = BruteForce.LeastEnclosedHeight(enclosure, 400);

            Assert.True(leastHeight.HasValue, Inv($"The oracle found nothing within its height bound for {enclosure.Value}."));
            Assert.True(terminal.IsEnclosed);
            Assert.Equal(leastHeight!.Value, terminal.Height);
        }
    }

    [Fact]
    public void Search_TerminalIsNotAlwaysTheLeastHeightEnclosedRational()
    {
        // SPEC section 3 states the least-height claim unconditionally. It is false when the
        // enclosure contains two or more integers, because at denominator one the sweep only ever
        // considers the NEAREST integer, never the one of least height.
        //
        // [1, 2] contains both 1 and 2. The sweep takes round(3/2) = 2 away from zero, which is
        // enclosed, and stops - reporting height 2 while height 1 was available. Measured over
        // 551985 enclosures, every failure of the claim encloses at least two integers and none
        // has MaxError below 1/2; for b >= 2 the enclosure must fit inside a gap of the Farey
        // sequence of order b-1, which is too narrow to hold two rationals of denominator b.
        Approximation enclosure = Approximation.Create(Ratio(3, 2), Ratio(1, 2));

        RationalCandidate terminal = Run(enclosure)[^1];

        Assert.True(terminal.IsEnclosed);
        Assert.Equal(Ratio(2, 1), terminal.Value);
        Assert.Equal(new BigInteger(2), terminal.Height);

        // ...while a lower-height rational was enclosed all along.
        Assert.True(enclosure.Contains(BigRational.One));
        Assert.Equal(BigInteger.One, BruteForce.LeastEnclosedHeight(enclosure, 50));
    }

    [Fact]
    public void Search_AlwaysTerminatesAtTheLeastEnclosedDenominator()
    {
        // The denominator bound is what section 1 says to report, and unlike the height claim it
        // holds without qualification: the sweep tries denominators in order and the nearest
        // rational of a denominator is enclosed whenever any rational of it is.
        foreach (Approximation enclosure in Targets())
        {
            RationalCandidate terminal = Run(enclosure)[^1];
            BigInteger? leastDenominator = BruteForce.LeastEnclosedDenominator(enclosure, 400);

            Assert.True(leastDenominator.HasValue, Inv($"The oracle found nothing within its denominator bound for {enclosure.Value}."));
            Assert.Equal(leastDenominator!.Value, terminal.Value.Denominator);
        }
    }

    [Fact]
    public void Search_LeavesNothingEnclosedBelowItsTerminatingDenominator()
    {
        // The exhaustiveness claim, brute-forced: every rational of every smaller denominator
        // misses the enclosure, for any numerator.
        foreach (Approximation enclosure in Targets())
        {
            BigInteger terminalDenominator = Run(enclosure)[^1].Value.Denominator;

            for (int q = 1; q < terminalDenominator; q++)
            {
                BigInteger smallest = BigRational.Round(enclosure.Lower * q, MidpointRounding.ToPositiveInfinity);
                BigInteger largest = BigRational.Round(enclosure.Upper * q, MidpointRounding.ToNegativeInfinity);

                Assert.True(smallest > largest, Inv($"Denominator {q} encloses {smallest}/{q}, below the terminating {terminalDenominator}."));
            }
        }
    }

    // ---------- the invariants the interface promises ----------

    [Fact]
    public void Search_YieldsCandidatesOfStrictlyIncreasingHeight()
    {
        // Asserted, not assumed. The spec argues it only for targets above one, and worries that
        // reduction could drop a candidate's height below its sweep index.
        foreach (Approximation enclosure in Targets())
        {
            BigInteger? previous = null;
            foreach (RationalCandidate candidate in Run(enclosure))
            {
                if (previous is BigInteger earlier)
                {
                    Assert.True(candidate.Height > earlier, Inv($"Height did not increase: {earlier} then {candidate.Height} for {enclosure.Value}."));
                }

                previous = candidate.Height;
            }
        }
    }

    [Fact]
    public void Search_YieldsCandidatesOfStrictlyIncreasingDenominator()
    {
        foreach (Approximation enclosure in Targets())
        {
            BigInteger? previous = null;
            foreach (RationalCandidate candidate in Run(enclosure))
            {
                if (previous is BigInteger earlier)
                {
                    Assert.True(candidate.Value.Denominator > earlier, Inv($"Denominator did not increase: {earlier} then {candidate.Value.Denominator}."));
                }

                previous = candidate.Value.Denominator;
            }
        }
    }

    [Fact]
    public void Search_YieldsOnlyCandidatesAlreadyInLowestTermsAtTheirSweepIndex()
    {
        // The reduction worry cannot materialise, and the reason is not luck: if a candidate at
        // index b reduced to denominator q < b, then index q had already produced the nearest
        // rational of denominator q, which is at least as close - so the reduced candidate would
        // not have been a strict improvement and would never be yielded.
        //
        // Checked here without reference to the sweep's rounding: each yielded candidate must be
        // at least as close to the target as both of its neighbours at its own denominator.
        foreach (Approximation enclosure in Targets())
        {
            foreach (RationalCandidate candidate in Run(enclosure))
            {
                Assert.True(
                    BruteForce.IsNearestOfItsDenominator(candidate.Value, enclosure.Value),
                    Inv($"{candidate.Value} is not the nearest rational of its own denominator to {enclosure.Value}."));
            }
        }
    }

    [Fact]
    public void Search_YieldsStrictlyImprovingCandidates()
    {
        foreach (Approximation enclosure in Targets())
        {
            BigRational? previous = null;
            foreach (RationalCandidate candidate in Run(enclosure))
            {
                BigRational distance = BigRational.Abs(candidate.Value - enclosure.Value);

                if (previous is BigRational earlier)
                {
                    Assert.True(distance < earlier, Inv($"Distance did not improve: {earlier} then {distance}."));
                }

                previous = distance;
            }
        }
    }

    [Fact]
    public void Search_OrdersTheSameWayUnderAllThreeDistanceMeasures()
    {
        // "Strictly better" is measured against the enclosure's Value. The choice turns out to be
        // unobservable: for a candidate outside the enclosure, MinDistance and MaxDistance differ
        // from the distance to Value by exactly MaxError, so all three induce the same order - and
        // every comparison the search makes is against a candidate outside the enclosure.
        foreach (Approximation enclosure in Targets())
        {
            BigRational? previousMin = null;
            BigRational? previousMax = null;

            foreach (RationalCandidate candidate in Run(enclosure))
            {
                if (previousMin is BigRational earlierMin)
                {
                    Assert.True(candidate.MinDistance < earlierMin);
                }

                if (previousMax is BigRational earlierMax)
                {
                    Assert.True(candidate.MaxDistance < earlierMax);
                }

                previousMin = candidate.MinDistance;
                previousMax = candidate.MaxDistance;
            }
        }
    }

    [Fact]
    public void Search_EndsAtTheFirstEnclosedCandidateAndYieldsNothingAfterIt()
    {
        foreach (Approximation enclosure in Targets())
        {
            List<RationalCandidate> candidates = Run(enclosure);

            Assert.NotEmpty(candidates);
            Assert.True(candidates[^1].IsEnclosed, Inv($"The search ended without an enclosed candidate for {enclosure.Value}."));

            for (int i = 0; i < candidates.Count - 1; i++)
            {
                Assert.False(candidates[i].IsEnclosed, Inv($"Candidate {candidates[i].Value} was enclosed but the search continued."));
            }
        }
    }

    // ---------- rounding ----------

    [Fact]
    public void Search_UsesANearestRounding()
    {
        // The exhaustiveness claim rests on this and nothing else. A directed rounding would keep
        // returning answers and quietly stop justifying them.
        Assert.Equal(MidpointRounding.AwayFromZero, DenominatorSweep.NumeratorRounding);

        Approximation enclosure = Approximation.Create(Ratio(22, 7), Ratio(1, 1000));
        foreach (RationalCandidate candidate in Run(enclosure))
        {
            Assert.True(BruteForce.IsNearestOfItsDenominator(candidate.Value, enclosure.Value));
        }
    }

    [Fact]
    public void Search_BreaksTiesAwayFromZero()
    {
        // 3/2 at denominator one is exactly halfway between 1 and 2. Either is sound, but the
        // oracle has to be deterministic, so the mode is fixed and stated.
        List<RationalCandidate> positive = Run(Approximation.Create(Ratio(3, 2), Ratio(1, 100)));
        Assert.Equal(Ratio(2, 1), positive[0].Value);

        // Away from zero is symmetric under negation, so the negative twin mirrors exactly.
        List<RationalCandidate> negative = Run(Approximation.Create(Ratio(-3, 2), Ratio(1, 100)));
        Assert.Equal(Ratio(-2, 1), negative[0].Value);

        Assert.Equal(positive.Count, negative.Count);
        for (int i = 0; i < positive.Count; i++)
        {
            Assert.Equal(BigRational.Negate(positive[i].Value), negative[i].Value);
        }
    }

    // ---------- termination ----------

    [Fact]
    public void Search_OnAnExactEnclosure_FindsTheValueAtItsOwnDenominator()
    {
        // Even with no error to exploit the sweep terminates, because the enclosure's value is a
        // BigRational and therefore itself rational.
        List<RationalCandidate> candidates = Run(Approximation.Exact(Ratio(5, 13)));

        Assert.Equal(Ratio(5, 13), candidates[^1].Value);
        Assert.Equal(new BigInteger(13), candidates[^1].Value.Denominator);
        Assert.Equal(BigRational.Zero, candidates[^1].MaxDistance);
    }

    [Fact]
    public void Search_OnAnExactZero_TerminatesImmediately()
    {
        List<RationalCandidate> candidates = Run(Approximation.Exact(BigRational.Zero));

        Assert.Single(candidates);
        Assert.Equal(BigRational.Zero, candidates[0].Value);
        Assert.Equal(BigInteger.One, candidates[0].Height);
    }

    [Fact]
    public void Search_OnAnEnclosureSpanningZero_TerminatesAtZero()
    {
        List<RationalCandidate> candidates = Run(Approximation.Create(Ratio(1, 10), BigRational.One));

        Assert.Single(candidates);
        Assert.Equal(BigRational.Zero, candidates[0].Value);
    }

    [Fact]
    public void Search_OnANegativeTarget_MirrorsThePositiveOne()
    {
        List<RationalCandidate> positive = Run(Approximation.Create(Ratio(22, 7), Ratio(1, 100)));
        List<RationalCandidate> negative = Run(Approximation.Create(Ratio(-22, 7), Ratio(1, 100)));

        Assert.Equal(positive.Count, negative.Count);
        for (int i = 0; i < positive.Count; i++)
        {
            Assert.Equal(BigRational.Negate(positive[i].Value), negative[i].Value);
            Assert.Equal(positive[i].Height, negative[i].Height);
        }
    }

    [Fact]
    public void Search_FindsTheExpectedCandidatesForAKnownTarget()
    {
        // Worked by hand: 22/7 +/- 1/100 is [313/100, 317/100].
        //   b = 1  round(22/7) = 3      3/1,   distance 1/7
        //   b = 2  round(44/7) = 6      3/1    equal, dropped
        //   b = 3  round(66/7) = 9      3/1    equal, dropped
        //   b = 4  round(88/7) = 13     13/4,  distance 3/28   improvement
        //   b = 5  round(110/7) = 16    16/5,  distance 2/35   improvement
        //   b = 6  round(132/7) = 19    19/6,  distance 1/42   improvement
        //   b = 7  round(154/7) = 22    22/7,  distance 0      improvement, and enclosed
        List<RationalCandidate> candidates = Run(Approximation.Create(Ratio(22, 7), Ratio(1, 100)));

        Assert.Equal(
            new[] { Ratio(3, 1), Ratio(13, 4), Ratio(16, 5), Ratio(19, 6), Ratio(22, 7) },
            candidates.Select(c => c.Value));
    }

    // ---------- laziness ----------

    [Fact]
    public void Search_IsLazy()
    {
        // 999983 is prime, so the exact enclosure below only terminates at denominator 999983.
        // Taking two candidates returns immediately; an eager implementation would grind through
        // a million denominators before this test could look at anything.
        Approximation slow = Approximation.Exact(new BigRational(999982, 999983));

        IEnumerable<RationalCandidate> search = new DenominatorSweep().Search(slow);

        Assert.IsNotAssignableFrom<ICollection<RationalCandidate>>(search);
        Assert.Equal(2, search.Take(2).Count());
    }

    private static int EnclosedIntegerCount(Approximation enclosure)
    {
        BigInteger smallest = BigRational.Round(enclosure.Lower, MidpointRounding.ToPositiveInfinity);
        BigInteger largest = BigRational.Round(enclosure.Upper, MidpointRounding.ToNegativeInfinity);

        return smallest > largest ? 0 : (int)(largest - smallest + BigInteger.One);
    }
}
