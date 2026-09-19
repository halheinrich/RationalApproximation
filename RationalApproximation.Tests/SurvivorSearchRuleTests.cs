using System.Numerics;

using static HalHeinrich.Numerics.Tests.SurvivorSearchContractTests;
using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The rational-target rules on <see cref="SurvivorSearch"/> where the test calls no walk: the
/// strictness of the predicate and the refusals. Every test that checks a rule against what a walk
/// returns is in <see cref="SurvivorSearchContractTests"/>, so it runs against each implementation;
/// these would only repeat themselves there.
/// </summary>
public class SurvivorSearchRuleTests
{
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
}
