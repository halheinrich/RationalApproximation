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
            () => new List<BigRational>(SurvivorSearch.Survivors(enclosures, denominatorBound)),
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
            () => new List<BigRational>(SurvivorSearch.Survivors([Enclosure(6, 1, 1, 10)], bound).Take(3)),
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
            () => SurvivorSearch.Survivors([], 10));

        Assert.Equal("enclosures", thrown.ParamName);
    }

    [Fact]
    public void Survivors_WithNullEnclosures_ThrowAtTheCallAndNotAtTheFirstStep()
    {
        // Not merely that it throws, but that it throws here. An iterator method defers its whole
        // body to the first MoveNext, which would surface an argument fault at some later foreach
        // with nothing left to say which call caused it.
        Assert.Throws<ArgumentNullException>(() => SurvivorSearch.Survivors(null!, 10));
    }

    [Fact]
    public void Survivors_WithANegativeBound_ThrowAtTheCall()
    {
        ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => SurvivorSearch.Survivors([Enclosure(6, 1, 1, 10)], -1));

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
