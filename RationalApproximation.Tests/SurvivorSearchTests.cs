using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="SurvivorSearch"/>, the refutation search. Its answers are checked against fixtures
/// computed by hand and against <see cref="BruteForce.SurvivorsByIntersection"/>, which reaches
/// them by intersecting the intervals instead of seeding a walk and testing each candidate.
/// </summary>
public class SurvivorSearchTests
{
    /// <summary>The walk under test, and the only implementation there is.</summary>
    private static readonly DenominatorWalk Walk = new();

    /// <summary>
    /// Enclosure sets spanning the shapes that change the answer, each with the denominator bound
    /// it is searched under: one enclosure and several, nesting and not, wide and exact, positive
    /// and negative and straddling zero, disjoint, and one whose endpoints are integers. The
    /// bounds are chosen to keep every survivor set small enough to compare element by element.
    /// </summary>
    private static (Approximation[] Enclosures, int Bound)[] Families() =>
    [
        ([Enclosure(6, 1, 1, 10)], 10),
        ([Enclosure(6, 1, 1, 10)], 60),
        ([Enclosure(6, 1, 1, 10), Enclosure(61, 10, 1, 10)], 10),
        ([Enclosure(6, 1, 1, 10), Enclosure(599, 100, 1, 100)], 20),
        ([Enclosure(1, 1, 4, 1)], 6),
        ([Enclosure(-22, 7, 1, 100)], 30),
        ([Approximation.Exact(Ratio(1, 7))], 14),
        ([Approximation.Exact(BigRational.Zero)], 5),
        ([Enclosure(0, 1, 1, 1000)], 12),
        ([Enclosure(355, 113, 1, 100000)], 200),
        ([Enclosure(6, 1, 1, 10), Enclosure(7, 1, 1, 10)], 10),
        ([Enclosure(3, 2, 1, 2)], 8),
        ([Enclosure(6, 1, 1, 1), Enclosure(61, 10, 3, 20), Enclosure(6, 1, 1, 10)], 10),
    ];

    /// <summary>Builds an enclosure from two exact rationals, keeping the fixtures free of decimals.</summary>
    private static Approximation Enclosure(int value, int valueDenominator, int error, int errorDenominator) =>
        Approximation.Create(Ratio(value, valueDenominator), Ratio(error, errorDenominator));

    /// <summary>Runs a search to completion under the shared budget. Reasoning in <see cref="BoundedSearch"/>.</summary>
    private static List<BigRational> Run(IEnumerable<Approximation> enclosures, BigInteger denominatorBound) =>
        BoundedSearch.CompleteWithin(
            () => new List<BigRational>(Walk.Survivors(enclosures, denominatorBound)),
            Inv($"A survivor search to denominator {denominatorBound}"));

    // ---------- the four fixtures, computed by hand ----------

    [Fact]
    public void Survivors_ForOneEnclosure_AreEveryRationalOfTheBoundItAdmits()
    {
        // 6 +/- 1/10 is [59/10, 61/10]. At denominator 1 that holds 6; at 10 it holds 59, 60 and
        // 61 over 10, of which 60/10 is 6 again and so is not proposed a second time. Denominators
        // 2 through 9 each hold only the corresponding multiple of 6, which is 6 again.
        Assert.Equal(
            new[] { Ratio(6, 1), Ratio(59, 10), Ratio(61, 10) },
            Run([Enclosure(6, 1, 1, 10)], 10));
    }

    [Fact]
    public void Survivors_UnderATighterBound_LoseWhatTheBoundExcludesRatherThanWhatIsRefuted()
    {
        // The same enclosure. What removes 59/10 and 61/10 here is the bound and not a refutation:
        // the enclosure still admits both, and a caller reading this result as evidence against
        // them would be reading a budget as a proof.
        Assert.Equal(new[] { Ratio(6, 1) }, Run([Enclosure(6, 1, 1, 10)], 3));
    }

    [Fact]
    public void Survivors_AcrossEnclosuresThatDoNotNest_AreTheIntersectionAndNotTheLast()
    {
        Approximation first = Enclosure(6, 1, 1, 10);
        Approximation second = Enclosure(61, 10, 1, 10);

        // The second enclosure alone admits seven candidates of denominator at or below 10, five
        // of which the first has already refuted. Neither enclosure contains the other, so no
        // single one of them is the answer and an implementation filtering on the last is wrong
        // here rather than merely weaker.
        Assert.Equal(
            new[]
            {
                Ratio(6, 1), Ratio(31, 5), Ratio(37, 6), Ratio(43, 7),
                Ratio(49, 8), Ratio(55, 9), Ratio(61, 10),
            },
            Run([second], 10));

        Assert.Equal(new[] { Ratio(6, 1), Ratio(61, 10) }, Run([first, second], 10));
    }

    [Fact]
    public void Survivors_AcrossANarrowerSecondEnclosure_AreOnlyWhatBothAdmit()
    {
        // 599/100 +/- 1/100 is [299/50, 6], which meets the first enclosure at 6 alone among
        // rationals of denominator at or below 20.
        Assert.Equal(
            new[] { Ratio(6, 1) },
            Run([Enclosure(6, 1, 1, 10), Enclosure(599, 100, 1, 100)], 20));
    }

    [Fact]
    public void Survivors_CanBeRefutedByAnEnclosureThatIsNeitherTheSeedNorTheLast()
    {
        // Fixture C above cannot see this and neither can any pair, which a mutation run found the
        // expensive way: the walk is seeded from the narrowest enclosure, so with two of them
        // "test the seed and the last" already covers both and an implementation ignoring the
        // middle passes. Three are needed, with the sole refuter in the middle.
        //
        // The narrowest here is the last, at half-width 1/10, so it seeds the walk. 61/10 +/- 3/20
        // is [119/20, 25/4], which is the only one of the three excluding 59/10 - the first is
        // 6 +/- 1 and contains everything in sight.
        Approximation[] enclosures =
        [
            Enclosure(6, 1, 1, 1),
            Enclosure(61, 10, 3, 20),
            Enclosure(6, 1, 1, 10),
        ];

        Assert.Equal(new[] { Ratio(6, 1), Ratio(61, 10) }, Run(enclosures, 10));

        // Without the middle enclosure, 59/10 stands. That is what makes the assertion above a
        // statement about the middle one rather than about the pair around it.
        Assert.Equal(
            new[] { Ratio(6, 1), Ratio(59, 10), Ratio(61, 10) },
            Run([enclosures[0], enclosures[2]], 10));
    }

    // ---------- the property the whole formulation rests on ----------

    [Fact]
    public void Survivors_ShrinkAsEnclosuresAreAdded_AndNeverComeBack()
    {
        // Refutation is permanent, so the survivor set is monotone decreasing under adding
        // enclosures. This is what makes it evidence rather than a reading, and it is the claim a
        // trend row could not make.
        const int Bound = 40;

        Approximation[] chain =
        [
            Enclosure(6, 1, 1, 1),
            Enclosure(6, 1, 1, 10),
            Enclosure(599, 100, 1, 100),
            Enclosure(6, 1, 1, 1000),
        ];

        List<BigRational> previous = Run(chain[..1], Bound);
        var sizes = new List<int> { previous.Count };

        for (int length = 2; length <= chain.Length; length++)
        {
            List<BigRational> current = Run(chain[..length], Bound);

            Assert.All(
                current,
                survivor => Assert.True(
                    previous.Contains(survivor),
                    Inv($"{survivor} survived {length} enclosures but not the {length - 1} before them.")));

            sizes.Add(current.Count);
            previous = current;
        }

        // A chain that never shrank would satisfy the containment above and prove nothing, so the
        // fixture is required to exhibit the property it was chosen for.
        Assert.True(sizes[^1] < sizes[0], Inv($"The chain did not shrink: {string.Join(" -> ", sizes)}."));
        Assert.Equal(1, sizes[^1]);
    }

    // ---------- checked against an oracle that reaches the answer differently ----------

    [Fact]
    public void Survivors_AgreeWithTheIntersectionOracle()
    {
        // Two implementations agreeing is the strongest correctness evidence available here, and
        // it is only evidence where they are actually different code. The oracle never asks which
        // enclosure is narrowest and never tests a candidate against one, so a defect in the seed
        // or in the per-enclosure test cannot be mirrored in it.
        int compared = 0;

        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            List<BigRational> sorted = [.. Run(enclosures, bound)];
            sorted.Sort();

            Assert.Equal(BruteForce.SurvivorsByIntersection(enclosures, bound), sorted);
            compared++;
        }

        Assert.Equal(Families().Length, compared);
    }

    [Fact]
    public void Survivors_AreDistinct_SoNoRationalIsReportedOncePerSpelling()
    {
        // The oracle compares as a set and so cannot see a duplicate on its own. This is what
        // makes that comparison a real check rather than one a repeated 6 would pass.
        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            List<BigRational> found = Run(enclosures, bound);
            Assert.Equal(found.Count, found.Distinct().Count());
        }
    }

    [Fact]
    public void Survivors_AreYieldedInNondecreasingDenominatorOrder()
    {
        int ordered = 0;

        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            List<BigRational> found = Run(enclosures, bound);

            for (int i = 1; i < found.Count; i++)
            {
                BigInteger previous = found[i - 1].Denominator;
                BigInteger current = found[i].Denominator;

                Assert.True(
                    previous <= current,
                    Inv($"{found[i]} follows {found[i - 1]}, so the denominators fell."));

                // Within one denominator the numerators ascend, so the values do.
                Assert.True(
                    previous < current || found[i - 1] < found[i],
                    Inv($"{found[i]} follows {found[i - 1]} at one denominator without increasing."));
            }

            ordered += found.Count;
        }

        // Fixtures that were all empty or all single would order trivially and prove nothing.
        Assert.True(ordered > 100, Inv($"Only {ordered} survivors were ordered."));
    }

    [Fact]
    public void Survivors_LieInEveryEnclosure_AndWithinTheBound()
    {
        // The contract restated as a predicate over the values returned, rather than over the
        // mechanism that produced them.
        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            foreach (BigRational survivor in Run(enclosures, bound))
            {
                Assert.True(survivor.Denominator <= bound, Inv($"{survivor} exceeds the bound {bound}."));
                Assert.All(
                    enclosures,
                    enclosure => Assert.True(
                        enclosure.Contains(survivor),
                        Inv($"{survivor} is outside the enclosure at {enclosure.Value}.")));
            }
        }
    }

    [Fact]
    public void Survivors_DoNotDependOnWhichEnclosureSeedsTheWalk()
    {
        // The narrowest enclosure is chosen to make the walk cheap, and that is a cost decision
        // rather than a correctness one. Reversing the order changes which enclosure a tie in
        // width resolves to, and must change nothing else.
        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            Approximation[] reversed = [.. enclosures.Reverse()];

            Assert.Equal(Run(enclosures, bound), Run(reversed, bound));
        }
    }

    // ---------- the settled decisions, each pinned by the case that defeated a draft ----------

    [Fact]
    public void Survivors_ReportARationalOnce_NotOncePerDenominatorThatSpellsIt()
    {
        // The failure that made canonical form a settled decision: 6 is spelled (6q, q) at every
        // denominator q, and a draft taking the pairs as they came reported 1500 survivors which
        // were 1500 spellings of one.
        Assert.Equal(new[] { Ratio(6, 1) }, Run([Approximation.Exact(Ratio(6, 1))], 1500));
    }

    [Fact]
    public void Survivors_CanBeTakenAFewAtATime_WithoutWalkingToTheBound()
    {
        // Laziness, against a bound no eager walk could reach. The first three survivors of
        // 6 +/- 1/10 are settled by denominator 10, so this returns at once if candidates are
        // walked one at a time, and never if they are collected before being returned.
        BigInteger bound = BigInteger.Pow(10, 30);

        List<BigRational> first = BoundedSearch.CompleteWithin(
            () => new List<BigRational>(Walk.Survivors([Enclosure(6, 1, 1, 10)], bound).Take(3)),
            "A truncated survivor search");

        Assert.Equal(new[] { Ratio(6, 1), Ratio(59, 10), Ratio(61, 10) }, first);
    }

    [Fact]
    public void Survivors_ReadTheEnclosureSequenceExactlyOnce()
    {
        // The enclosures are copied at the call, so a caller may hand over a lazy sequence and a
        // search in progress cannot see it change underneath. Re-enumerating it once per candidate
        // would be both a cost and a correctness hazard, and neither shows up in the values.
        var counter = new CountingEnclosures([Enclosure(6, 1, 1, 10)]);

        Assert.Equal(new[] { Ratio(6, 1), Ratio(59, 10), Ratio(61, 10) }, Run(counter, 10));
        Assert.Equal(1, counter.Enumerations);
    }

    // ---------- edge cases ----------

    [Fact]
    public void Survivors_WithNoEnclosures_Throw_RatherThanReportingCompleteRefutation()
    {
        // With nothing to refute, every rational within the bound survives - infinitely many of
        // them, the numerator being unbounded. An empty result would say the opposite of the
        // truth, and it is the direction AGENTS.md's report-the-bound rule forbids.
        ArgumentException thrown = Assert.Throws<ArgumentException>(
            () => Walk.Survivors([], 10));

        Assert.Equal("enclosures", thrown.ParamName);
    }

    [Fact]
    public void Survivors_WithNullEnclosures_ThrowAtTheCallAndNotAtTheFirstStep()
    {
        // Not merely that it throws, but that it throws here. An iterator method defers its whole
        // body to the first MoveNext, which would surface an argument fault at some later foreach
        // with nothing left to say which call caused it.
        Assert.Throws<ArgumentNullException>(() => Walk.Survivors(null!, 10));
    }

    [Fact]
    public void Survivors_WithANegativeBound_ThrowAtTheCall()
    {
        ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => Walk.Survivors([Enclosure(6, 1, 1, 10)], -1));

        Assert.Equal("denominatorBound", thrown.ParamName);
    }

    [Fact]
    public void Survivors_AtBoundZero_AreNone_BecauseNothingWasExamined()
    {
        // A denominator is positive, so a bound of zero admits no candidate at all. This is an
        // empty result meaning "nothing was examined", not "everything was refuted" - the two are
        // indistinguishable in the value and are distinguished by the bound reported beside it.
        Assert.Empty(Run([Enclosure(6, 1, 1, 10)], 0));
    }

    [Fact]
    public void Survivors_AtBoundOne_AreTheIntegersTheEnclosuresHold()
    {
        Assert.Equal(new[] { Ratio(6, 1) }, Run([Enclosure(6, 1, 1, 10)], 1));

        // And an enclosure holding no integer yields nothing at that bound, though it holds a
        // rational nine denominators further out.
        Assert.Empty(Run([Enclosure(61, 10, 1, 100)], 1));
        Assert.Equal(new[] { Ratio(61, 10) }, Run([Enclosure(61, 10, 1, 100)], 10));
    }

    [Fact]
    public void Survivors_OfDisjointEnclosures_AreNoneAtAnyBound()
    {
        // Two enclosures that cannot both hold anything. Nothing survives, and here the emptiness
        // does mean complete refutation.
        Assert.Empty(Run([Enclosure(6, 1, 1, 10), Enclosure(7, 1, 1, 10)], 60));
    }

    [Fact]
    public void Survivors_OfAnExactEnclosure_AreItsValueOnceTheBoundReachesItsDenominator()
    {
        Approximation exact = Approximation.Exact(Ratio(1, 7));

        // A zero-width interval holds exactly one rational, so below that rational's own
        // denominator an exact enclosure refutes everything the bound offers.
        Assert.Empty(Run([exact], 6));
        Assert.Equal(new[] { Ratio(1, 7) }, Run([exact], 7));
        Assert.Equal(new[] { Ratio(1, 7) }, Run([exact], 700));
    }

    [Fact]
    public void Survivors_OfANegativeEnclosure_MirrorThePositiveTwin()
    {
        // The sign is carried by the numerator, so a negative target's answer is the positive
        // one's negated - in the reverse order within each denominator, since the values ascend.
        Assert.Equal(
            new[] { Ratio(-6, 1), Ratio(-61, 10), Ratio(-59, 10) },
            Run([Enclosure(-6, 1, 1, 10)], 10));
    }

    [Fact]
    public void Survivors_OfAWideEnclosure_IncludeEveryIntegerItStraddles()
    {
        // 1 +/- 4 is [-3, 5]. Every integer in it survives, and the first yielded is -3/1 at
        // height 3 rather than the 0/1 of height 1 it also holds - the promised order being a
        // denominator order and not a height order.
        Assert.Equal(
            new[]
            {
                Ratio(-3, 1), Ratio(-2, 1), Ratio(-1, 1), Ratio(0, 1), Ratio(1, 1),
                Ratio(2, 1), Ratio(3, 1), Ratio(4, 1), Ratio(5, 1),
            },
            Run([Enclosure(1, 1, 4, 1)], 1));
    }

    // ---------- reachability and isolation: SPEC-rational-ratio.md § 2's two rules ----------
    //
    // Every rule here is tested against what Survivors returns, never against its own formula: the
    // rules are claims about the search, and a test that recomputed 1/(2Qb) would agree with any
    // wrong copy of it. That is how the isolation rule stayed wrong three times in prose - every
    // check ever run had an integer target, where dropping b changes nothing.

    /// <summary>
    /// Targets above denominator 1, where the isolation rule's <c>b</c> is visible. The first two
    /// are the n = 12 and n = 16 answers of <c>SPEC-rational-ratio.md</c> § 1, chosen for their
    /// denominators 691 and 3617 - the size a real control carries - and the rest are small ones on
    /// both sides of zero and on both sides of one.
    /// </summary>
    private static BigRational[] FractionalTargets() =>
    [
        new(638_512_875, 691),
        new(325_641_566_250, 3617),
        Ratio(7, 3),
        Ratio(-5, 3),
        Ratio(22, 7),
        Ratio(1, 2),
    ];

    /// <summary>The small <see cref="FractionalTargets"/>, for sweeps too wide to afford the large two.</summary>
    private static BigRational[] SmallFractionalTargets() => [Ratio(7, 3), Ratio(-5, 3), Ratio(22, 7), Ratio(1, 2)];

    /// <summary>Integer targets, where the isolation bound is exact. Zero and a negative included.</summary>
    private static BigRational[] IntegerTargets() => [Ratio(6, 1), Ratio(90, 1), Ratio(-3, 1), BigRational.Zero];

    /// <summary>Just inside an exclusive bound - close enough that a rival at the bound is in reach of a looser rule.</summary>
    private static BigRational JustBelow(BigRational bound) => bound * Ratio(999, 1000);

    /// <summary>Just outside an exclusive bound.</summary>
    private static BigRational JustAbove(BigRational bound) => bound * Ratio(1001, 1000);

    /// <summary>
    /// Enclosures of one half-width that all contain the target: with it at either end, at the
    /// quarter points, and centred. The ends are the worst case the isolation rule is stated over.
    /// A centred enclosure alone is the best case and pins nothing, which is the mistake the
    /// umbrella's first check of this rule made.
    /// </summary>
    private static Approximation[] EnclosuresContaining(BigRational target, BigRational halfWidth) =>
    [
        Approximation.Create(target + halfWidth, halfWidth),
        Approximation.Create(target + (halfWidth / 2), halfWidth),
        Approximation.Create(target, halfWidth),
        Approximation.Create(target - (halfWidth / 2), halfWidth),
        Approximation.Create(target - halfWidth, halfWidth),
    ];

    /// <summary>
    /// The least bound, at or above the target's denominator, at which a rival sits exactly
    /// <c>1/(Q*b)</c> from the target on the given side - one of the residue classes in which
    /// <c>SPEC-rational-ratio.md</c> § 2 says the isolation bound is attained.
    /// </summary>
    /// <remarks>
    /// Found by scanning for an integral <c>p = (Q*a + side)/b</c>, which is the defining equation
    /// <c>p*b - Q*a = side</c> solved for <c>p</c>, rather than by a modular inverse - so the
    /// fixture is its own definition. The scan ends within <c>b</c> steps, since <c>a</c> and
    /// <c>b</c> are coprime.
    /// </remarks>
    private static (BigInteger Bound, BigRational Rival) AttainingBound(BigRational target, int side)
    {
        BigInteger a = target.Numerator;
        BigInteger b = target.Denominator;

        for (BigInteger q = b; ; q++)
        {
            BigInteger scaled = (q * a) + side;
            if (BigInteger.Remainder(scaled, b).IsZero)
            {
                return (q, new BigRational(scaled / b, q));
            }
        }
    }

    /// <summary>
    /// The bounds a fractional target's soundness is checked at: its own denominator and one past
    /// it, twice it and one past that, and the two bounds at which the isolation bound is attained
    /// - the sharpest, since there a rival sits exactly where a looser rule would reach it.
    /// </summary>
    private static BigInteger[] SoundnessBounds(BigRational target)
    {
        BigInteger b = target.Denominator;

        return
        [
            .. new[] { b, b + 1, (2 * b) + 1, AttainingBound(target, -1).Bound, AttainingBound(target, +1).Bound }
                .Distinct()
                .Order(),
        ];
    }

    /// <summary>Runs one enclosure's search and requires the target, and nothing else, to survive it.</summary>
    private static void AssertAlone(BigRational target, BigInteger bound, Approximation enclosure)
    {
        List<BigRational> survivors = Run([enclosure], bound);

        Assert.True(
            survivors.Count == 1 && survivors[0] == target,
            Inv($"{target} at Q = {bound} under {enclosure.Value} +/- {enclosure.MaxError}: ") +
            Inv($"{survivors.Count} survivors, starting {string.Join(", ", survivors.Take(3))}."));
    }

    /// <summary>A search's survivors in ascending order, for comparing as a set.</summary>
    private static List<BigRational> SortedRun(Approximation enclosure, BigInteger bound)
    {
        List<BigRational> sorted = Run([enclosure], bound);
        sorted.Sort();
        return sorted;
    }

    [Fact]
    public void IsReachable_TurnsOnTheTargetsOwnDenominator_AsTheSearchDoes()
    {
        // An exact enclosure on the target holds nothing else, so whether the search returns the
        // target is purely whether it proposed it - the question the predicate answers, reached
        // without its formula.
        int reached = 0;
        int missed = 0;
        BigRational[] targets = [.. IntegerTargets(), .. FractionalTargets()];

        foreach (BigRational target in targets)
        {
            BigInteger b = target.Denominator;

            foreach (BigInteger bound in new[] { b - 1, b, b + 1 })
            {
                bool expected = bound >= b;
                bool searched = Run([Approximation.Exact(target)], bound).Contains(target);

                Assert.Equal(expected, SurvivorSearch.IsReachable(target, bound));
                Assert.True(
                    searched == expected,
                    Inv($"The search {(searched ? "proposed" : "did not propose")} {target} at Q = {bound}."));

                if (expected)
                {
                    reached++;
                }
                else
                {
                    missed++;
                }
            }
        }

        Assert.True(reached > 0 && missed > 0, Inv($"{reached} reached and {missed} missed: one side is unexercised."));
    }

    [Fact]
    public void IsReachable_AtBoundZero_IsFalse_BecauseThatIsWhatTheSearchReturns()
    {
        // Survivors accepts a bound of zero and examines nothing there, so the predicate answers
        // rather than refusing: one type, one definition of a valid bound.
        BigRational[] targets = [BigRational.Zero, Ratio(6, 1), Ratio(7, 3)];

        foreach (BigRational target in targets)
        {
            Assert.False(SurvivorSearch.IsReachable(target, 0));
            Assert.Empty(Run([Approximation.Exact(target)], 0));
        }
    }

    [Fact]
    public void Survivors_JustBelowTheIsolationBound_AreExactlyTheTarget_WhereverTheCentreSits()
    {
        // Soundness, worst case, above denominator 1 - the case that tells 1/(2Qb) from 1/(2Q).
        // Every enclosure contains the target, and two of the five have it at an end.
        int searched = 0;

        foreach (BigRational target in FractionalTargets())
        {
            foreach (BigInteger bound in SoundnessBounds(target))
            {
                BigRational halfWidth = JustBelow(SurvivorSearch.ExclusiveIsolationBound(target, bound));
                Assert.True(SurvivorSearch.IsIsolated(target, bound, halfWidth));

                foreach (Approximation enclosure in EnclosuresContaining(target, halfWidth))
                {
                    AssertAlone(target, bound, enclosure);
                    searched++;
                }
            }
        }

        Assert.True(searched >= FractionalTargets().Length * 4 * 5, Inv($"Only {searched} searches ran."));
    }

    [Fact]
    public void Survivors_AtTheIsolationBound_AdmitARival_WhenTheTargetIsAnInteger()
    {
        // At denominator 1 the bound is exact: at the bound itself a rival target +/- 1/Q is in
        // reach of an enclosure with the target at the facing end, and just below it is not. That
        // is why the inequality is strict - and why this test alone cannot see b dropped.
        foreach (BigRational target in IntegerTargets())
        {
            foreach (BigInteger bound in new BigInteger[] { 1, 2, 7, 64, 100 })
            {
                BigRational exclusiveBound = SurvivorSearch.ExclusiveIsolationBound(target, bound);
                BigRational step = new(BigInteger.One, bound);

                Assert.False(SurvivorSearch.IsIsolated(target, bound, exclusiveBound));
                Assert.Equal(
                    new[] { target, target + step },
                    SortedRun(Approximation.Create(target + exclusiveBound, exclusiveBound), bound));
                Assert.Equal(
                    new[] { target - step, target },
                    SortedRun(Approximation.Create(target - exclusiveBound, exclusiveBound), bound));

                BigRational inside = JustBelow(exclusiveBound);
                Assert.True(SurvivorSearch.IsIsolated(target, bound, inside));
                AssertAlone(target, bound, Approximation.Create(target + inside, inside));
                AssertAlone(target, bound, Approximation.Create(target - inside, inside));
            }
        }
    }

    [Fact]
    public void IsolationBound_AboveDenominatorOne_IsAttained_AtTheBoundsWhereARivalSitsClosest()
    {
        // The only test that sees a bound too SMALL. A smaller bound is always sound, so every
        // soundness test passes it, and 1/(2Q*b^2) equals 1/(2Q*b) at b = 1, so every integer test
        // passes it too. What fails it is a rival exactly 2x the bound from the target - which
        // exists only at bounds chosen from the residue classes, so they are chosen, never assumed.
        // At an arbitrary bound above denominator 1 no rival need be there, and asserting one would
        // be false.
        int attained = 0;

        foreach (BigRational target in FractionalTargets())
        {
            foreach (int side in new[] { -1, +1 })
            {
                (BigInteger bound, BigRational rival) = AttainingBound(target, side);

                // The fixture's own claims: in lowest terms at that bound, and on the side asked for.
                Assert.Equal(bound, rival.Denominator);
                Assert.Equal(side, (rival - target).Sign);

                BigRational exclusiveBound = SurvivorSearch.ExclusiveIsolationBound(target, bound);

                // The bound is exactly half the rival's distance - pinned by geometry the fixture
                // found independently, not by restating the formula.
                Assert.Equal(BigRational.Abs(rival - target), 2 * exclusiveBound);

                List<BigRational> expected = [target, rival];
                expected.Sort();

                Assert.Equal(
                    expected,
                    SortedRun(Approximation.Create(target + (side * exclusiveBound), exclusiveBound), bound));
                attained++;
            }
        }

        Assert.Equal(FractionalTargets().Length * 2, attained);
    }

    [Fact]
    public void IsIsolated_AgreesWithTheSearch_OnBothSidesOfTheBound()
    {
        // The rule is a claim about what the search returns, so it is tested against the search.
        // Wherever it says yes, every enclosure of that half-width containing the target leaves the
        // target alone. At an integer target the converse holds too; above denominator 1 a no is
        // not a claim that a rival survives, since the bound is sufficient and not exact there.
        int yes = 0;
        int no = 0;
        BigRational[] targets = [.. IntegerTargets(), .. SmallFractionalTargets()];

        foreach (BigRational target in targets)
        {
            BigInteger b = target.Denominator;

            for (BigInteger bound = b; bound <= (3 * b) + 2; bound++)
            {
                BigRational exclusiveBound = SurvivorSearch.ExclusiveIsolationBound(target, bound);

                BigRational[] halfWidths =
                [
                    BigRational.Zero,
                    exclusiveBound / 2,
                    JustBelow(exclusiveBound),
                    exclusiveBound,
                    JustAbove(exclusiveBound),
                    2 * exclusiveBound,
                ];

                foreach (BigRational halfWidth in halfWidths)
                {
                    if (SurvivorSearch.IsIsolated(target, bound, halfWidth))
                    {
                        foreach (Approximation enclosure in EnclosuresContaining(target, halfWidth))
                        {
                            AssertAlone(target, bound, enclosure);
                        }

                        yes++;
                    }
                    else
                    {
                        if (b.IsOne)
                        {
                            List<BigRational> crowded = Run([Approximation.Create(target + halfWidth, halfWidth)], bound);
                            Assert.True(
                                crowded.Count > 1,
                                Inv($"IsIsolated said no to {target} at Q = {bound}, +/- {halfWidth}, and nothing else survived."));
                        }

                        no++;
                    }
                }
            }
        }

        // A grid that never said yes would pass the first branch vacuously, and one that never
        // said no would never exercise the strictness.
        Assert.True(yes > 0 && no > 0, Inv($"{yes} yes and {no} no: one answer never occurred."));
    }

    [Fact]
    public void IsIsolated_IsStrictlyBelowTheExclusiveBound()
    {
        BigRational[] targets = [Ratio(6, 1), Ratio(7, 3), new(638_512_875, 691)];

        foreach (BigRational target in targets)
        {
            BigInteger bound = target.Denominator + 3;
            BigRational exclusiveBound = SurvivorSearch.ExclusiveIsolationBound(target, bound);

            Assert.True(SurvivorSearch.IsIsolated(target, bound, BigRational.Zero));
            Assert.True(SurvivorSearch.IsIsolated(target, bound, JustBelow(exclusiveBound)));
            Assert.False(SurvivorSearch.IsIsolated(target, bound, exclusiveBound));
            Assert.False(SurvivorSearch.IsIsolated(target, bound, JustAbove(exclusiveBound)));
        }
    }

    [Fact]
    public void ExclusiveIsolationBound_ReachedByCoarsening_LosesTheProofOfIsolation_NotTheResult()
    {
        // The trap the name exists for. At an integer target under a power-of-two bound the
        // exclusive bound is itself a power of two, so an enclosure strictly inside it coarsens
        // onto it. What that loses is the PROOF: the half-width now held no longer establishes
        // isolation, since an enclosure of that half-width with the target at its end admits a
        // rival. The result itself stands, because coarsening keeps the centre the narrower radius
        // placed - which is exactly why a caller must ask of the half-width held, and not assume.
        BigRational target = Ratio(6, 1);
        BigInteger bound = 64;
        BigRational exclusiveBound = SurvivorSearch.ExclusiveIsolationBound(target, bound);

        Assert.True(IsPowerOfTwo(exclusiveBound));

        BigRational narrower = exclusiveBound * Ratio(3, 4);
        Approximation refined = Approximation.Create(target + narrower, narrower);
        Approximation coarsened = refined.Coarsen();

        Assert.True(SurvivorSearch.IsIsolated(target, bound, refined.MaxError));
        Assert.Equal(exclusiveBound, coarsened.MaxError);
        Assert.False(SurvivorSearch.IsIsolated(target, bound, coarsened.MaxError));

        AssertAlone(target, bound, coarsened);
        Assert.Equal(
            new[] { target, Ratio(385, 64) },
            SortedRun(Approximation.Create(target + exclusiveBound, exclusiveBound), bound));
    }

    [Fact]
    public void ANegativeBound_IsRefusedIdenticallyByEveryMemberTakingOne()
    {
        // One definition of a valid bound for the whole type, observable as one refusal.
        BigRational target = Ratio(7, 3);

        ArgumentOutOfRangeException[] refusals =
        [
            Assert.Throws<ArgumentOutOfRangeException>(() => Walk.Survivors([Enclosure(6, 1, 1, 10)], -1)),
            Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorSearch.IsReachable(target, -1)),
            Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorSearch.ExclusiveIsolationBound(target, -1)),
            Assert.Throws<ArgumentOutOfRangeException>(() => SurvivorSearch.IsIsolated(target, -1, BigRational.Zero)),
        ];

        Assert.All(refusals, refusal => Assert.Equal("denominatorBound", refusal.ParamName));
        Assert.Single(refusals.Select(refusal => refusal.Message).Distinct());
    }

    [Fact]
    public void Isolation_AtABoundTheTargetCannotReach_IsRefusedRatherThanAnsweredNo()
    {
        // Below the target's denominator the survivor set is empty, not crowded, so there is no
        // true answer to "is it isolated". A no would report the crowded failure for the empty
        // one, collapsing the two directions SPEC § 2 keeps apart.
        string negativeMessage = Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorSearch.IsReachable(Ratio(6, 1), -1)).Message;

        foreach ((BigRational target, BigInteger bound) in new (BigRational, BigInteger)[]
        {
            (Ratio(6, 1), 0),
            (Ratio(7, 3), 2),
            (new(325_641_566_250, 3617), 3616),
        })
        {
            ArgumentOutOfRangeException fromBound = Assert.Throws<ArgumentOutOfRangeException>(
                () => SurvivorSearch.ExclusiveIsolationBound(target, bound));
            ArgumentOutOfRangeException fromPredicate = Assert.Throws<ArgumentOutOfRangeException>(
                () => SurvivorSearch.IsIsolated(target, bound, BigRational.Zero));

            Assert.Equal("denominatorBound", fromBound.ParamName);
            Assert.Equal("denominatorBound", fromPredicate.ParamName);
            Assert.NotEqual(negativeMessage, fromBound.Message);
        }
    }

    [Fact]
    public void IsIsolated_WithANegativeHalfWidth_Throws()
    {
        ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorSearch.IsIsolated(Ratio(7, 3), 5, Ratio(-1, 100)));

        Assert.Equal("halfWidth", thrown.ParamName);
    }

    /// <summary>
    /// An enclosure sequence that counts how often it is enumerated, for the one claim the values
    /// returned cannot carry.
    /// </summary>
    private sealed class CountingEnclosures(Approximation[] enclosures) : IEnumerable<Approximation>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<Approximation> GetEnumerator()
        {
            Enumerations++;
            return ((IEnumerable<Approximation>)enclosures).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
