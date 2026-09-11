using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="HeightSweep"/>, the height-ordered searcher. Its claims are checked against
/// <see cref="BruteForce"/>, which is built from the definitions, and cross-checked against
/// <see cref="DenominatorSweep"/>, which orders differently.
/// </summary>
public class HeightSweepTests
{
    /// <summary>
    /// The oracle's search bound. Every fixture below terminates at a height well inside it; the
    /// tests assert the oracle actually found something rather than letting a silent miss pass.
    /// </summary>
    private const int OracleBound = 400;

    /// <summary>
    /// Enclosures spanning the shapes that change the search: above one and below one, since the
    /// axis switches between them; exactly one and exactly minus one, the boundary; negative;
    /// exact; straddling zero; wide enough that <c>1/1</c> is already inside; wide enough to hold
    /// two integers; and a deep target of magnitude near 26.
    /// </summary>
    private static Approximation[] Targets() =>
    [
        Approximation.Create(Ratio(6, 1), Ratio(1, 100)),
        Approximation.Create(Ratio(22, 7), Ratio(1, 100)),
        Approximation.Create(Ratio(-22, 7), Ratio(1, 100)),
        Approximation.Create(Ratio(15, 4), Ratio(1, 100)),
        Approximation.Create(Ratio(-3, 2), Ratio(1, 100)),
        Approximation.Create(Ratio(355, 113), Ratio(1, 100000)),
        Approximation.Create(Ratio(7919, 307), Ratio(1, 100)),
        Approximation.Create(Ratio(3, 2), Ratio(1, 2)),
        Approximation.Create(Ratio(12, 1), Ratio(1, 1)),
        Approximation.Create(Ratio(5, 1), Ratio(9, 1)),
        Approximation.Create(Ratio(17, 50), Ratio(1, 1000)),
        Approximation.Create(Ratio(1, 10), Ratio(1, 1)),
        Approximation.Create(BigRational.One, Ratio(1, 100)),
        Approximation.Create(Ratio(-1, 1), Ratio(1, 100)),
        Approximation.Exact(Ratio(-8, 5)),
        Approximation.Exact(Ratio(5, 13)),
        Approximation.Exact(BigRational.Zero),
    ];

    /// <summary>Runs a search to completion under the shared bounds. Reasoning in <see cref="BoundedSearch"/>.</summary>
    private static List<RationalCandidate> Run(Approximation enclosure) =>
        BoundedSearch.RunToCompletion(new HeightSweep(), enclosure);

    // ---------- the ordering this type exists for ----------

    [Fact]
    public void Search_OnAnIntegerTarget_TriesEverySimplerIntegerFirst()
    {
        // The reason this type exists. DenominatorSweep proposes 6/1 at denominator 1 and stops,
        // so a step-by-step exhibit of its run shows one candidate and no refutation. Height order
        // shows six candidates and five refutations.
        Approximation enclosure = Approximation.Create(Ratio(6, 1), Ratio(1, 100));

        Assert.Equal(
            new[] { Ratio(1, 1), Ratio(2, 1), Ratio(3, 1), Ratio(4, 1), Ratio(5, 1), Ratio(6, 1) },
            Run(enclosure).Select(c => c.Value));

        Assert.Equal(
            new[] { Ratio(6, 1) },
            BoundedSearch.RunToCompletion(new DenominatorSweep(), enclosure).Select(c => c.Value));
    }

    [Fact]
    public void Search_FindsTheExpectedCandidatesForAKnownTarget()
    {
        // Worked by hand: 22/7 +/- 1/100 is [313/100, 317/100]. At each numerator a the closest
        // denominator is the better of floor(a/x) and its successor, clamped up to 1.
        //   a = 1  b = 1   1/1,   distance 15/7
        //   a = 2  b = 1   2/1,   distance 8/7    improvement
        //   a = 3  b = 1   3/1,   distance 1/7    improvement
        //   a = 4  b = 1   4/1,   distance 6/7    dropped
        //   ...
        //   a = 13 b = 4   13/4,  distance 3/28   improvement
        //   a = 16 b = 5   16/5,  distance 2/35   improvement
        //   a = 19 b = 6   19/6,  distance 1/42   improvement
        //   a = 22 b = 7   22/7,  distance 0      improvement, and enclosed
        List<RationalCandidate> candidates = Run(Approximation.Create(Ratio(22, 7), Ratio(1, 100)));

        Assert.Equal(
            new[] { Ratio(1, 1), Ratio(2, 1), Ratio(3, 1), Ratio(13, 4), Ratio(16, 5), Ratio(19, 6), Ratio(22, 7) },
            candidates.Select(c => c.Value));
    }

    // ---------- the least-height claim, against an independent oracle ----------

    [Fact]
    public void Search_TerminatesAtTheLeastHeightEnclosedRational()
    {
        // Unconditional, unlike DenominatorSweep's version of the same claim - including on the
        // wide enclosures that are exactly where its version fails.
        int checkedTargets = 0;

        foreach (Approximation enclosure in Targets())
        {
            RationalCandidate terminal = Run(enclosure)[^1];
            BigInteger? leastHeight = BruteForce.LeastEnclosedHeight(enclosure, OracleBound);

            Assert.True(leastHeight.HasValue, Inv($"The oracle found nothing within its height bound for {enclosure.Value}."));
            Assert.True(terminal.IsEnclosed);
            Assert.Equal(leastHeight!.Value, terminal.Height);
            checkedTargets++;
        }

        Assert.Equal(Targets().Length, checkedTargets);
    }

    [Fact]
    public void Search_TerminatesAtALowerHeightThanTheSweepWhereTheEnclosureHoldsTwoIntegers()
    {
        // The first of the two findings recorded against DenominatorSweep: at denominator 1 it
        // takes only the NEAREST integer, so an enclosure holding two can report the one further
        // from zero. Height order has no such exception, because it reaches the low integers
        // before the high ones.
        foreach ((Approximation enclosure, BigRational byHeight, BigRational bySweep) in new[]
        {
            (Approximation.Create(Ratio(3, 2), Ratio(1, 2)), Ratio(1, 1), Ratio(2, 1)),
            (Approximation.Create(Ratio(12, 1), Ratio(1, 1)), Ratio(11, 1), Ratio(12, 1)),
            (Approximation.Create(Ratio(5, 1), Ratio(9, 1)), Ratio(1, 1), Ratio(5, 1)),
        })
        {
            Assert.Equal(byHeight, Run(enclosure)[^1].Value);
            Assert.Equal(bySweep, BoundedSearch.RunToCompletion(new DenominatorSweep(), enclosure)[^1].Value);
        }
    }

    // ---------- the cross-check: three searches over one claim ----------

    [Fact]
    public void Search_AgreesWithTheDenominatorSweepAndTheOracleOnNarrowEnclosures()
    {
        // Two independent implementations agreeing is the strongest correctness evidence available
        // here. BruteForce is the arbiter of the two: it enumerates EVERY reduced rational of each
        // height rather than one per index, so it cannot miss a candidate the other two order past
        // - which is precisely the failure mode neither of them could detect in the other.
        //
        // Restricted to enclosures holding fewer than two integers, and to targets above one.
        // Above one the two searches are genuinely different code; at or below one HeightSweep
        // DELEGATES to DenominatorSweep, so agreement there is a tautology and proves nothing. The
        // two-integer case is excluded because they provably differ there - it has its own test
        // above. Measured over 5387 enclosures above one, those are the only disagreements.
        int compared = 0;

        foreach (Approximation enclosure in Targets())
        {
            if (!HeightSweep.SearchesNumerators(enclosure) || EnclosedIntegerCount(enclosure) >= 2)
            {
                continue;
            }

            RationalCandidate byHeight = Run(enclosure)[^1];
            RationalCandidate bySweep = BoundedSearch.RunToCompletion(new DenominatorSweep(), enclosure)[^1];
            BigInteger? leastHeight = BruteForce.LeastEnclosedHeight(enclosure, OracleBound);

            Assert.Equal(bySweep.Value, byHeight.Value);
            Assert.True(leastHeight.HasValue, Inv($"The oracle found nothing within its height bound for {enclosure.Value}."));
            Assert.Equal(leastHeight!.Value, byHeight.Height);
            compared++;
        }

        Assert.True(compared >= 6, Inv($"Only {compared} targets exercised the cross-check."));
    }

    [Fact]
    public void Search_AgreesWithTheDenominatorSweepOnADeepTarget()
    {
        // A ratio of two primes near 26, chosen for the depth of its continued fraction rather
        // than for being any particular quantity: this layer holds no concrete constants, and a
        // decimal truncation of an interesting real would be one wearing a disguise.
        //
        // The cost of height order falls straight out of this. Neither enclosure here contains an
        // integer, and wherever at most one is enclosed both searches end at the SAME rational,
        // the sweep having reached its denominator and this having reached its numerator, so the
        // ratio of the indices they examine is the target itself - 129 against 5 here, and 1006
        // against 39 one target tighter. With two or more integers enclosed they can part; see
        // HeightSweep's remarks.
        foreach ((BigRational error, BigRational expected) in new[]
        {
            (Ratio(1, 100), Ratio(129, 5)),
            (Ratio(1, 10000), Ratio(1006, 39)),
        })
        {
            Approximation enclosure = Approximation.Create(Ratio(7919, 307), error);

            RationalCandidate byHeight = Run(enclosure)[^1];
            RationalCandidate bySweep = BoundedSearch.RunToCompletion(new DenominatorSweep(), enclosure)[^1];

            Assert.Equal(expected, byHeight.Value);
            Assert.Equal(expected, bySweep.Value);
            Assert.Equal(BigInteger.Abs(expected.Numerator), byHeight.Height);
        }
    }

    // ---------- the axis ----------

    [Fact]
    public void SearchesNumerators_IsTrueExactlyWhereTheMagnitudeExceedsOne()
    {
        // The boundary belongs to the denominator axis deliberately: at exactly one the two axes
        // agree, and routing it left keeps every division by the target on the side where the
        // target cannot be zero or near it.
        foreach ((BigRational value, bool expected) in new[]
        {
            (Ratio(22, 7), true),
            (Ratio(-22, 7), true),
            (Ratio(101, 100), true),
            (BigRational.One, false),
            (Ratio(-1, 1), false),
            (Ratio(99, 100), false),
            (BigRational.Zero, false),
        })
        {
            Assert.Equal(expected, HeightSweep.SearchesNumerators(Approximation.Create(value, Ratio(1, 1000))));
        }

        // The axis is a property of the value alone, not of how wide the enclosure is.
        Assert.True(HeightSweep.SearchesNumerators(Approximation.Create(Ratio(3, 2), Ratio(100, 1))));
        Assert.False(HeightSweep.SearchesNumerators(Approximation.Exact(BigRational.Zero)));
    }

    [Fact]
    public void Search_BelowOne_IsTheDenominatorSweepExactly()
    {
        // Not merely equivalent - the same code. Stated as a test so that a later session that
        // reimplements the branch has to notice it is claiming independence it does not have.
        foreach (Approximation enclosure in Targets())
        {
            if (HeightSweep.SearchesNumerators(enclosure))
            {
                continue;
            }

            Assert.Equal(
                BoundedSearch.RunToCompletion(new DenominatorSweep(), enclosure).Select(c => c.Value),
                Run(enclosure).Select(c => c.Value));
        }
    }

    // ---------- the denominator choice, and the tie ----------

    [Fact]
    public void BestDenominator_MinimisesTheDistanceRatherThanRoundingTheQuotient()
    {
        // The defect this method exists to avoid. At a fixed DENOMINATOR the closest numerator is
        // a nearest-rounding of x*b, because the numerator enters linearly. At a fixed NUMERATOR
        // it is not: a/b is a hyperbola in b. Rounding 10 / (29/10) gives 3, at distance 13/30;
        // 4 is closer, at 2/5. A rounded quotient here would forfeit exhaustiveness exactly as a
        // directed rounding would on the other axis, and just as quietly.
        BigRational target = Ratio(29, 10);

        Assert.Equal(new BigInteger(4), HeightSweep.BestDenominator(new BigInteger(10), target));
        Assert.True(Ratio(13, 30) > Ratio(2, 5));
        Assert.Equal(Ratio(13, 30), BigRational.Abs(Ratio(10, 3) - target));
        Assert.Equal(Ratio(2, 5), BigRational.Abs(Ratio(10, 4) - target));
    }

    [Fact]
    public void BestDenominator_BreaksTiesTowardsTheCandidateFurtherFromZero()
    {
        // 5/1 and 5/2 are equidistant from 15/4, and both are reduced. Either is sound; an oracle
        // must be deterministic. The rule is the one AwayFromZero encodes on the axis the sibling
        // varies - take the candidate further from zero, here the smaller denominator - and
        // phrasing it that way is what makes it symmetric under negation.
        Assert.Equal(BigRational.Abs(Ratio(5, 1) - Ratio(15, 4)), BigRational.Abs(Ratio(5, 2) - Ratio(15, 4)));

        Assert.Equal(BigInteger.One, HeightSweep.BestDenominator(new BigInteger(5), Ratio(15, 4)));
        Assert.Equal(BigInteger.One, HeightSweep.BestDenominator(new BigInteger(-5), Ratio(-15, 4)));
    }

    [Fact]
    public void BestDenominator_NeverReturnsLessThanOne()
    {
        // Every numerator below the target's magnitude has an ideal denominator under one, and
        // there is no such denominator to return. The clamp is what makes 1/1, 2/1, ... 5/1 the
        // opening of a search against 6.
        foreach (int numerator in new[] { 1, 2, 3, 4, 5 })
        {
            Assert.Equal(BigInteger.One, HeightSweep.BestDenominator(new BigInteger(numerator), Ratio(6, 1)));
        }
    }

    [Fact]
    public void BestDenominator_AgreesWithTheOracleAcrossEveryYieldedCandidate()
    {
        // Checked without reference to how the denominator was chosen: each yielded candidate must
        // be at least as close to the target as the rationals of the same numerator at its two
        // neighbouring denominators.
        foreach (Approximation enclosure in Targets())
        {
            if (!HeightSweep.SearchesNumerators(enclosure))
            {
                continue;
            }

            foreach (RationalCandidate candidate in Run(enclosure))
            {
                Assert.True(
                    BruteForce.IsNearestOfItsNumerator(candidate.Value, enclosure.Value),
                    Inv($"{candidate.Value} is not the nearest rational of its own numerator to {enclosure.Value}."));
            }
        }
    }

    // ---------- the invariants the interface promises ----------

    [Fact]
    public void Search_YieldsCandidatesOfStrictlyIncreasingHeight()
    {
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

    [Fact]
    public void Search_IsLazy()
    {
        // 999983 is prime, so this exact enclosure only terminates at numerator 999983. Taking two
        // candidates returns immediately; an eager implementation would grind through a million
        // numerators before this test could look at anything.
        Approximation slow = Approximation.Exact(new BigRational(999983, 7));

        IEnumerable<RationalCandidate> search = new HeightSweep().Search(slow);

        Assert.IsNotAssignableFrom<ICollection<RationalCandidate>>(search);
        Assert.Equal(2, search.Take(2).Count());
    }

    // ---------- the reduced-pair skip ----------

    [Fact]
    public void Search_YieldsOnlyCandidatesAlreadyInLowestTermsAtTheirNumerator()
    {
        // The skip is an early-out, not a correctness device: a non-reduced pair equals one with a
        // smaller numerator, whose own best denominator was at least as close, so the improvement
        // filter would drop it anyway. What the skip buys is that a yielded candidate's height IS
        // its search index, which is what makes the enumeration height order by construction
        // rather than by argument.
        foreach (Approximation enclosure in Targets())
        {
            if (!HeightSweep.SearchesNumerators(enclosure))
            {
                continue;
            }

            foreach (RationalCandidate candidate in Run(enclosure))
            {
                Assert.Equal(
                    BigInteger.One,
                    BigInteger.GreatestCommonDivisor(BigInteger.Abs(candidate.Value.Numerator), candidate.Value.Denominator));
                Assert.Equal(BigInteger.Abs(candidate.Value.Numerator), candidate.Height);
            }
        }
    }

    [Fact]
    public void Search_SkipsTheNonReducedPairsItsAxisProduces()
    {
        // Against a target near 26 the numerators 36, 38, 40, ... all pair with denominator 2, so
        // 36/2 would revisit 18/1 - the pairs the skip exists for, and the first three it meets.
        BigRational target = Ratio(7919, 307);

        foreach (int numerator in new[] { 36, 38, 40 })
        {
            BigInteger paired = HeightSweep.BestDenominator(new BigInteger(numerator), target);

            Assert.Equal(new BigInteger(2), paired);
            Assert.NotEqual(BigInteger.One, BigInteger.GreatestCommonDivisor(new BigInteger(numerator), paired));

            // Why skipping them cannot lose anything, which is also why the skip is an early-out
            // and not a correctness device: the reduced form has a smaller numerator, and that
            // numerator's own best denominator is at least as close as this pair.
            BigInteger halved = numerator / 2;
            BigRational reduced = new(halved, HeightSweep.BestDenominator(halved, target));

            Assert.True(
                BigRational.Abs(reduced - target) <= BigRational.Abs(new BigRational(numerator, paired) - target),
                Inv($"{reduced} is further from {target} than the pair {numerator}/{paired} it stands in for."));
        }
    }

    // ---------- symmetry and termination ----------

    [Fact]
    public void Search_OnANegativeTarget_MirrorsThePositiveOne()
    {
        foreach ((BigRational value, BigRational error) in new[]
        {
            (Ratio(22, 7), Ratio(1, 100)),
            (Ratio(15, 4), Ratio(1, 100)),
            (Ratio(7919, 307), Ratio(1, 100)),
            (Ratio(1, 3), Ratio(1, 1000)),
        })
        {
            List<RationalCandidate> positive = Run(Approximation.Create(value, error));
            List<RationalCandidate> negative = Run(Approximation.Create(BigRational.Negate(value), error));

            Assert.Equal(positive.Count, negative.Count);
            for (int i = 0; i < positive.Count; i++)
            {
                Assert.Equal(BigRational.Negate(positive[i].Value), negative[i].Value);
                Assert.Equal(positive[i].Height, negative[i].Height);
            }
        }
    }

    [Fact]
    public void Search_OnAnExactEnclosure_FindsTheValueAtItsOwnNumerator()
    {
        // The termination argument. Even with no error to exploit, the enclosure's value is a
        // BigRational n/d in lowest terms, so at numerator |n| the closest rational of that
        // numerator is the value itself.
        List<RationalCandidate> candidates = Run(Approximation.Exact(Ratio(-8, 5)));

        Assert.Equal(Ratio(-8, 5), candidates[^1].Value);
        Assert.Equal(new BigInteger(8), candidates[^1].Height);
        Assert.Equal(BigRational.Zero, candidates[^1].MaxDistance);
    }

    [Fact]
    public void Search_AtExactlyOne_TerminatesImmediatelyOnEitherSign()
    {
        foreach (BigRational value in new[] { BigRational.One, Ratio(-1, 1) })
        {
            List<RationalCandidate> candidates = Run(Approximation.Create(value, Ratio(1, 100)));

            Assert.Single(candidates);
            Assert.Equal(value, candidates[0].Value);
            Assert.Equal(BigInteger.One, candidates[0].Height);
        }
    }

    [Fact]
    public void Search_OnAnEnclosureWideEnoughToHoldOne_TerminatesAtHeightOne()
    {
        // Above one, an enclosure reaching down to one necessarily contains one, so the first
        // candidate the numerator axis proposes is already the answer. This is the case that makes
        // the least-height claim unconditional rather than qualified.
        foreach (Approximation enclosure in new[]
        {
            Approximation.Create(Ratio(5, 1), Ratio(9, 1)),
            Approximation.Create(Ratio(3, 2), Ratio(1, 2)),
            Approximation.Create(Ratio(-3, 2), Ratio(1, 2)),
        })
        {
            List<RationalCandidate> candidates = Run(enclosure);

            Assert.Single(candidates);
            Assert.Equal(BigInteger.One, candidates[0].Height);
        }
    }

}
