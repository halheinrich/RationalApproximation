using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="DenominatorWalk"/>, the reference: the contract, inherited, and the order this walk
/// promises on top of it.
/// </summary>
public sealed class DenominatorWalkTests : SurvivorSearchContractTests
{
    /// <inheritdoc/>
    protected override SurvivorSearch Search { get; } = new DenominatorWalk();

    [Fact]
    public void Survivors_AreYieldedInNondecreasingDenominatorOrder()
    {
        int ordered = 0;

        foreach ((Approximation[] enclosures, int bound) in Families())
        {
            IReadOnlyList<BigRational> found = Run(enclosures, bound);

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
    public void Survivors_ComeSimplestFirst_WhichIsNotValueOrder()
    {
        // The order a caller picks this walk for. Against 6 +/- 1/10 at a bound of 10 it yields
        // the integer before the two tenths either side of it; FareyWalk yields the same three
        // left to right.
        Assert.Equal(new[] { Ratio(6, 1), Ratio(59, 10), Ratio(61, 10) }, Run([Enclosure(6, 1, 1, 10)], 10));
    }

    [Fact]
    public void Survivors_AreInDenominatorOrder_WhichIsNotHeightOrder()
    {
        // 1 +/- 4 is [-3, 5]. The first yielded is -3/1 at height 3 rather than the 0/1 of height 1
        // it also holds - the promised order being a denominator order and not a height order.
        Assert.Equal(
            new[]
            {
                Ratio(-3, 1), Ratio(-2, 1), Ratio(-1, 1), Ratio(0, 1), Ratio(1, 1),
                Ratio(2, 1), Ratio(3, 1), Ratio(4, 1), Ratio(5, 1),
            },
            Run([Enclosure(1, 1, 4, 1)], 1));
    }

    [Fact]
    public void Survivors_CanBeTakenAFewAtATime_WithoutWalkingToTheBound()
    {
        // Laziness, against a bound no eager walk could reach. The first three survivors of
        // 6 +/- 1/10 are settled by denominator 10, so this returns at once if candidates are
        // walked one at a time, and never if they are collected before being returned.
        BigInteger bound = BigInteger.Pow(10, 30);

        List<BigRational> first = BoundedSearch.CompleteWithin(
            () => new List<BigRational>(Search.Survivors([Enclosure(6, 1, 1, 10)], bound).Take(3)),
            "A truncated survivor search");

        Assert.Equal(new[] { Ratio(6, 1), Ratio(59, 10), Ratio(61, 10) }, first);
    }
}
