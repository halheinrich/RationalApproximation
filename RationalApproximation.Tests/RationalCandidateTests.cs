using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="RationalCandidate"/>: the naive height, and the distance interval an enclosure
/// permits between a candidate and the unknown.
/// </summary>
public class RationalCandidateTests
{
    private static readonly Approximation EightToTwelve = Approximation.Create(Ratio(10, 1), Ratio(2, 1));

    // ---------- height ----------

    [Fact]
    public void Height_IsTakenFromTheReducedFraction_NotFromTheSweepIndex()
    {
        // A sweep at denominator 2 yielding 12/2 carries height 6, not 12.
        RationalCandidate candidate = RationalCandidate.Against(BigRational.Create(12, 2), EightToTwelve);

        Assert.Equal(new BigInteger(6), candidate.Height);
        Assert.Equal(Ratio(6, 1), candidate.Value);
    }

    [Fact]
    public void Height_IsTheLargerOfAbsoluteNumeratorAndDenominator()
    {
        Assert.Equal(new BigInteger(22), HeightOf(Ratio(22, 7)));
        Assert.Equal(new BigInteger(7), HeightOf(Ratio(3, 7)));
        Assert.Equal(new BigInteger(22), HeightOf(Ratio(-22, 7)));
        Assert.Equal(new BigInteger(7), HeightOf(Ratio(-3, 7)));
    }

    [Fact]
    public void Height_OfAnIntegerIsItsMagnitude_AndOfZeroIsOne()
    {
        Assert.Equal(new BigInteger(945), HeightOf(Ratio(945, 1)));
        Assert.Equal(new BigInteger(945), HeightOf(Ratio(-945, 1)));

        // Zero reduces to 0/1, whose height is the denominator.
        Assert.Equal(BigInteger.One, HeightOf(BigRational.Zero));
    }

    [Fact]
    public void Height_MatchesTheControlsFromTheSpec()
    {
        // The even-case targets of section 1 are integers, so their heights are themselves - not
        // a shared denominator of one.
        foreach (int control in new[] { 6, 90, 945, 9450, 93555 })
        {
            Assert.Equal(new BigInteger(control), HeightOf(Ratio(control, 1)));
        }
    }

    // ---------- the distance interval ----------

    [Fact]
    public void Distances_BelowTheEnclosure_RunToTheNearAndFarEndpoints()
    {
        RationalCandidate candidate = RationalCandidate.Against(Ratio(5, 1), EightToTwelve);

        Assert.Equal(Ratio(3, 1), candidate.MinDistance);
        Assert.Equal(Ratio(7, 1), candidate.MaxDistance);
        Assert.False(candidate.IsEnclosed);
    }

    [Fact]
    public void Distances_AboveTheEnclosure_RunToTheNearAndFarEndpoints()
    {
        RationalCandidate candidate = RationalCandidate.Against(Ratio(15, 1), EightToTwelve);

        Assert.Equal(Ratio(3, 1), candidate.MinDistance);
        Assert.Equal(Ratio(7, 1), candidate.MaxDistance);
        Assert.False(candidate.IsEnclosed);
    }

    [Fact]
    public void Distances_InsideTheEnclosure_StartAtZero()
    {
        // The unknown may be the candidate itself, so nothing rules out a distance of zero.
        RationalCandidate candidate = RationalCandidate.Against(Ratio(9, 1), EightToTwelve);

        Assert.Equal(BigRational.Zero, candidate.MinDistance);
        Assert.Equal(Ratio(3, 1), candidate.MaxDistance);
        Assert.True(candidate.IsEnclosed);
    }

    [Fact]
    public void Distances_AtAnEndpoint_AreStillEnclosed()
    {
        RationalCandidate lower = RationalCandidate.Against(Ratio(8, 1), EightToTwelve);
        RationalCandidate upper = RationalCandidate.Against(Ratio(12, 1), EightToTwelve);

        Assert.True(lower.IsEnclosed);
        Assert.Equal(BigRational.Zero, lower.MinDistance);
        Assert.Equal(Ratio(4, 1), lower.MaxDistance);

        Assert.True(upper.IsEnclosed);
        Assert.Equal(BigRational.Zero, upper.MinDistance);
        Assert.Equal(Ratio(4, 1), upper.MaxDistance);
    }

    [Fact]
    public void Distances_AgainstAnExactEnclosure_CollapseToASinglePoint()
    {
        Approximation exact = Approximation.Exact(Ratio(22, 7));

        RationalCandidate onIt = RationalCandidate.Against(Ratio(22, 7), exact);
        Assert.Equal(BigRational.Zero, onIt.MinDistance);
        Assert.Equal(BigRational.Zero, onIt.MaxDistance);
        Assert.True(onIt.IsEnclosed);

        RationalCandidate offIt = RationalCandidate.Against(Ratio(3, 1), exact);
        Assert.Equal(Ratio(1, 7), offIt.MinDistance);
        Assert.Equal(Ratio(1, 7), offIt.MaxDistance);
        Assert.False(offIt.IsEnclosed);
    }

    [Fact]
    public void Distances_AreNeverNegativeAndNeverInverted()
    {
        foreach (Approximation enclosure in Enclosures())
        {
            foreach (BigRational value in PointsOf(enclosure))
            {
                foreach (BigRational offset in new[] { Ratio(-5, 1), BigRational.Zero, Ratio(7, 3) })
                {
                    RationalCandidate candidate = RationalCandidate.Against(value + offset, enclosure);

                    Assert.True(candidate.MinDistance.Sign >= 0);
                    Assert.True(candidate.MinDistance <= candidate.MaxDistance);
                }
            }
        }
    }

    [Fact]
    public void IsEnclosed_IsExactlyWhatTheEnclosureSays()
    {
        foreach (Approximation enclosure in Enclosures())
        {
            foreach (BigRational value in PointsOf(enclosure))
            {
                foreach (BigRational offset in new[] { Ratio(-3, 2), BigRational.Zero, Ratio(1, 8) })
                {
                    BigRational candidateValue = value + offset;
                    RationalCandidate candidate = RationalCandidate.Against(candidateValue, enclosure);

                    Assert.Equal(enclosure.Contains(candidateValue), candidate.IsEnclosed);
                }
            }
        }
    }

    [Fact]
    public void MinDistance_IsZeroExactlyWhenEnclosed()
    {
        foreach (Approximation enclosure in Enclosures())
        {
            foreach (BigRational value in PointsOf(enclosure))
            {
                foreach (BigRational offset in new[] { Ratio(-9, 4), BigRational.Zero, Ratio(5, 2) })
                {
                    RationalCandidate candidate = RationalCandidate.Against(value + offset, enclosure);

                    Assert.Equal(candidate.IsEnclosed, candidate.MinDistance.IsZero);
                }
            }
        }
    }

    // ---------- the default state ----------

    [Fact]
    public void Default_IsZeroJudgedAgainstTheExactlyZeroEnclosure()
    {
        RationalCandidate candidate = default;

        Assert.True(candidate.Value.IsZero);
        Assert.Equal(BigInteger.One, candidate.Height);
        Assert.Equal(BigRational.Zero, candidate.MinDistance);
        Assert.Equal(BigRational.Zero, candidate.MaxDistance);
        Assert.True(candidate.IsEnclosed);

        Assert.Equal(
            RationalCandidate.Against(BigRational.Zero, Approximation.Exact(BigRational.Zero)),
            candidate);
    }

    // ---------- equality ----------

    [Fact]
    public void Candidates_WithTheSameValueButDifferentEnclosures_AreNotEqual()
    {
        // They report different distances, so treating them as the same candidate would let one
        // enclosure's evidence be read off another's.
        RationalCandidate first = RationalCandidate.Against(Ratio(9, 1), EightToTwelve);
        RationalCandidate second = RationalCandidate.Against(Ratio(9, 1), Approximation.Exact(Ratio(9, 1)));

        Assert.Equal(first.Value, second.Value);
        Assert.NotEqual(first, second);
    }

    private static BigInteger HeightOf(BigRational value) =>
        RationalCandidate.Against(value, EightToTwelve).Height;
}
