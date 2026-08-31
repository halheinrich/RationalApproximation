using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The failure mode the whole design exists to prevent: every truncated series is a rational, so
/// the ratio of two of them is a rational, so a sweep run at zero error terminates at exactly that
/// ratio - reporting a precise, confident answer that says nothing about the constants and
/// everything about where the series were cut.
/// </summary>
/// <remarks>
/// <para>
/// The numbers here are derived and stated rather than quoted. The spec's worked example of this
/// point does not reproduce and is filed as halheinrich/Math#21, so nothing below is pinned to it.
/// </para>
/// <para>
/// Two truncations, chosen because they are short enough to check by hand:
/// </para>
/// <list type="bullet">
/// <item><description>pi by two Leibniz terms, <c>4*(1 - 1/3) = 8/3</c>.</description></item>
/// <item><description>zeta(3) by two terms of the defining sum, <c>1 + 1/8 = 9/8</c>.</description></item>
/// </list>
/// <para>
/// Their ratio is <c>(8/3)^3 / (9/8) = (512/27) * (8/9) = 4096/243</c>, which is about 16.86 -
/// nowhere near the true ratio of roughly 25.79, and already reduced, since 4096 is a power of two
/// and 243 a power of three.
/// </para>
/// </remarks>
public class TruncationArtifactTests
{
    /// <summary>pi truncated to two Leibniz terms.</summary>
    private static BigRational LeibnizTwoTerms => Ratio(4, 1) * (BigRational.One - Ratio(1, 3));

    /// <summary>zeta(3) truncated to two terms of the sum of reciprocal cubes.</summary>
    private static BigRational ZetaThreeTwoTerms => BigRational.One + Ratio(1, 8);

    [Fact]
    public void TheTwoTruncationsRatioToExactlyTheStatedValue()
    {
        // Pinning the derivation itself, so a later reader can check the arithmetic above without
        // trusting the prose.
        Assert.Equal(Ratio(8, 3), LeibnizTwoTerms);
        Assert.Equal(Ratio(9, 8), ZetaThreeTwoTerms);

        BigRational artifact = BigRational.Pow(LeibnizTwoTerms, 3) / ZetaThreeTwoTerms;

        Assert.Equal(Ratio(4096, 243), artifact);
    }

    [Fact]
    public void AZeroErrorEnclosure_MakesTheSweepDiscoverItsOwnTruncationPoint()
    {
        // Drop the error bounds and the ratio of two truncated series is just a rational, so the
        // sweep finds it exactly - at the denominator the truncation happened to produce, with a
        // four-figure height, and with total confidence.
        BigRational artifact = BigRational.Pow(LeibnizTwoTerms, 3) / ZetaThreeTwoTerms;

        List<RationalCandidate> candidates =
            BoundedSearch.RunToCompletion(new DenominatorSweep(), Approximation.Exact(artifact));

        RationalCandidate terminal = candidates[^1];

        Assert.Equal(Ratio(4096, 243), terminal.Value);
        Assert.Equal(new BigInteger(243), terminal.Value.Denominator);
        Assert.Equal(new BigInteger(4096), terminal.Height);
        Assert.Equal(BigRational.Zero, terminal.MaxDistance);
    }

    [Fact]
    public void CarryingTheTruncationErrors_TerminatesAtSomethingUselessButTrue()
    {
        // The same two truncations with their errors carried, through the shipped propagation.
        //
        //   Leibniz alternates, so its tail is bounded by the first omitted term: pi = 8/3 +/- 4/5.
        //   The tail of the cube sum is under 1/8 and the truncation under-estimates, so zeta(3)
        //   lies in [9/8, 5/4], which centred is 19/16 +/- 1/16.
        //
        // Pow(3) re-centres on the image of the interval, so the cube is 16256/675 +/- 6592/375
        // rather than (8/3)^3 with a bound bolted on - and the division bound then gives an
        // enclosure roughly [3.53, 37.03].
        Approximation pi = Approximation.Create(Ratio(8, 3), Ratio(4, 5));
        Approximation zeta = Approximation.Create(Ratio(19, 16), Ratio(1, 16));

        Approximation ratio = pi.Pow(3) / zeta;

        Assert.Equal(Ratio(16256, 675), pi.Pow(3).Value);
        Assert.Equal(Ratio(6592, 375), pi.Pow(3).MaxError);
        Assert.Equal(Ratio(260096, 12825), ratio.Value);
        Assert.Equal(Ratio(9668096, 577125), ratio.MaxError);

        // Honest, if useless: the enclosure really does contain the true ratio, which the spec
        // records as near 25.79. The zero-error answer above does not come close to it.
        Assert.True(ratio.Contains(Ratio(2579, 100)));
        Assert.False(Approximation.Exact(Ratio(4096, 243)).Contains(Ratio(2579, 100)));

        List<RationalCandidate> candidates = BoundedSearch.RunToCompletion(new DenominatorSweep(), ratio);
        RationalCandidate terminal = candidates[^1];

        Assert.Equal(Ratio(20, 1), terminal.Value);
        Assert.Equal(new BigInteger(20), terminal.Height);
        Assert.True(terminal.IsEnclosed);
    }

    [Fact]
    public void TheBoundIsWhatKeepsTheReportedHeightHonest()
    {
        // The contrast, in one place: the same two truncations give height 4096 with the bounds
        // dropped and height 20 with them carried. The bound is what makes the sweep terminate at
        // a sensible height. Making the answer CORRECT is the other job, and it belongs to the
        // trend matrix - which is why neither alone is enough.
        BigRational artifact = BigRational.Pow(LeibnizTwoTerms, 3) / ZetaThreeTwoTerms;

        BigInteger withoutBounds =
            BoundedSearch.RunToCompletion(new DenominatorSweep(), Approximation.Exact(artifact))[^1].Height;

        Approximation bounded =
            Approximation.Create(Ratio(8, 3), Ratio(4, 5)).Pow(3) / Approximation.Create(Ratio(19, 16), Ratio(1, 16));

        BigInteger withBounds = BoundedSearch.RunToCompletion(new DenominatorSweep(), bounded)[^1].Height;

        Assert.Equal(new BigInteger(4096), withoutBounds);
        Assert.Equal(new BigInteger(20), withBounds);
        Assert.True(withBounds < withoutBounds);
    }

    [Fact]
    public void RunningDeeperDiscoversADifferentTruncationPointWithEqualConfidence()
    {
        // Three Leibniz terms give 4*(1 - 1/3 + 1/5) = 52/15; three terms of the cube sum give
        // 1 + 1/8 + 1/27 = 251/216. Their ratio is a different rational entirely, and a zero-error
        // sweep lands on it just as precisely. Depth does not rescue a run that dropped its bounds.
        BigRational deeperPi = Ratio(4, 1) * (BigRational.One - Ratio(1, 3) + Ratio(1, 5));
        BigRational deeperZeta = BigRational.One + Ratio(1, 8) + Ratio(1, 27);

        Assert.Equal(Ratio(52, 15), deeperPi);
        Assert.Equal(Ratio(251, 216), deeperZeta);

        BigRational deeperArtifact = BigRational.Pow(deeperPi, 3) / deeperZeta;
        BigRational shallowArtifact = BigRational.Pow(LeibnizTwoTerms, 3) / ZetaThreeTwoTerms;

        Assert.NotEqual(shallowArtifact, deeperArtifact);

        RationalCandidate terminal =
            BoundedSearch.RunToCompletion(new DenominatorSweep(), Approximation.Exact(deeperArtifact))[^1];

        Assert.Equal(deeperArtifact, terminal.Value);
        Assert.Equal(BigRational.Zero, terminal.MaxDistance);
    }
}
