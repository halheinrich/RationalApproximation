using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Bound propagation for the arithmetic. The core of this file is the violation sweeps: for every
/// pair of fixture enclosures, every pair of sampled points is combined exactly and required to
/// lie inside the propagated enclosure. A bound that is too tight fails these; observing that a
/// bound held on one narrow example proves nothing.
/// </summary>
public class ApproximationArithmeticTests
{
    // ---------- violation sweeps ----------

    [Fact]
    public void Add_EnclosesEveryExactSumOfSampledPoints()
    {
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                Approximation result = a + b;
                AssertBoundIsWellFormed(result);

                foreach (BigRational x in PointsOf(a))
                {
                    foreach (BigRational y in PointsOf(b))
                    {
                        Assert.True(
                            result.Contains(x + y),
                            Inv($"{x} + {y} = {x + y} escaped [{result.Lower}, {result.Upper}]."));
                    }
                }
            }
        }
    }

    [Fact]
    public void Subtract_EnclosesEveryExactDifferenceOfSampledPoints()
    {
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                Approximation result = a - b;
                AssertBoundIsWellFormed(result);

                foreach (BigRational x in PointsOf(a))
                {
                    foreach (BigRational y in PointsOf(b))
                    {
                        Assert.True(
                            result.Contains(x - y),
                            Inv($"{x} - {y} = {x - y} escaped [{result.Lower}, {result.Upper}]."));
                    }
                }
            }
        }
    }

    [Fact]
    public void Multiply_EnclosesEveryExactProductOfSampledPoints()
    {
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                Approximation result = a * b;
                AssertBoundIsWellFormed(result);

                foreach (BigRational x in PointsOf(a))
                {
                    foreach (BigRational y in PointsOf(b))
                    {
                        Assert.True(
                            result.Contains(x * y),
                            Inv($"{x} * {y} = {x * y} escaped [{result.Lower}, {result.Upper}]."));
                    }
                }
            }
        }
    }

    [Fact]
    public void Divide_EnclosesEveryExactQuotientOfSampledPoints()
    {
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                if (!b.ExcludesZero)
                {
                    continue;
                }

                Approximation result = a / b;
                AssertBoundIsWellFormed(result);

                foreach (BigRational x in PointsOf(a))
                {
                    foreach (BigRational y in PointsOf(b))
                    {
                        Assert.True(
                            result.Contains(x / y),
                            Inv($"{x} / {y} = {x / y} escaped [{result.Lower}, {result.Upper}]."));
                    }
                }
            }
        }
    }

    [Fact]
    public void Pow_EnclosesEveryExactPowerOfSampledPoints()
    {
        foreach (Approximation a in Enclosures())
        {
            for (int exponent = -4; exponent <= 5; exponent++)
            {
                if (exponent < 0 && !a.ExcludesZero)
                {
                    continue;
                }

                Approximation result = a.Pow(exponent);
                AssertBoundIsWellFormed(result);

                foreach (BigRational x in PointsOf(a))
                {
                    BigRational image = BigRational.Pow(x, exponent);
                    Assert.True(
                        result.Contains(image),
                        Inv($"{x}^{exponent} = {image} escaped [{result.Lower}, {result.Upper}]."));
                }
            }
        }
    }

    // ---------- tightness: where the design promises the exact image, check it ----------

    [Fact]
    public void AddAndSubtract_AreExactOnTheEndpoints()
    {
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                Assert.Equal(a.Lower + b.Lower, (a + b).Lower);
                Assert.Equal(a.Upper + b.Upper, (a + b).Upper);

                Assert.Equal(a.Lower - b.Upper, (a - b).Lower);
                Assert.Equal(a.Upper - b.Lower, (a - b).Upper);
            }
        }
    }

    [Fact]
    public void Multiply_RadiusEqualsTheLargestCornerDeviation()
    {
        // The tightest symmetric enclosure of a product of intervals is centred on the product of
        // the centres with radius the largest deviation over the four corners. Computed here from
        // the corners rather than from the formula, so it is an independent check of the formula.
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                Approximation result = a * b;

                BigRational widest = BigRational.Zero;
                foreach (BigRational x in new[] { a.Lower, a.Upper })
                {
                    foreach (BigRational y in new[] { b.Lower, b.Upper })
                    {
                        BigRational deviation = BigRational.Abs((x * y) - result.Value);
                        if (deviation > widest)
                        {
                            widest = deviation;
                        }
                    }
                }

                Assert.Equal(widest, result.MaxError);
            }
        }
    }

    [Fact]
    public void Divide_RadiusEqualsTheLargestCornerDeviation()
    {
        // The specified division bound turns out to be not merely sound but exactly the tightest
        // symmetric enclosure of the quotient. Its numerator is the largest |x*b - y*a| over the
        // box and its denominator the smallest |y|*|b|, and a single corner attains both at once:
        // taking y to the endpoint nearest zero fixes the sign of the -a*(y - b) term, and x is
        // then free to align with it. Checked from the corners here rather than from the formula.
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                if (!b.ExcludesZero)
                {
                    continue;
                }

                Approximation result = a / b;

                BigRational widest = BigRational.Zero;
                foreach (BigRational x in new[] { a.Lower, a.Upper })
                {
                    foreach (BigRational y in new[] { b.Lower, b.Upper })
                    {
                        BigRational deviation = BigRational.Abs((x / y) - result.Value);
                        if (deviation > widest)
                        {
                            widest = deviation;
                        }
                    }
                }

                Assert.Equal(widest, result.MaxError);
            }
        }
    }

    [Fact]
    public void Multiply_SecondOrderTermIsLoadBearing()
    {
        // A first-order bound |a|*beta + |b|*alpha would give 1 here, and the true product of
        // [1, 3] and [1, 3] reaches 9, which is 5 +/- 4. Wide enclosures are what expose this.
        Approximation a = Approximation.Create(Ratio(2, 1), BigRational.One);
        Approximation result = a * a;

        Assert.Equal(Ratio(4, 1), result.Value);
        Assert.Equal(Ratio(5, 1), result.MaxError);
        Assert.True(result.Contains(Ratio(9, 1)));
        Assert.True(result.Contains(BigRational.One));
    }

    [Fact]
    public void Pow_AttainsBothEndpointsOfItsResult()
    {
        // The result is the exact image of the interval, so each endpoint must be the image of
        // some point of the input - an endpoint, or zero where an even power turns there.
        foreach (Approximation a in Enclosures())
        {
            for (int exponent = -4; exponent <= 5; exponent++)
            {
                if (exponent < 0 && !a.ExcludesZero)
                {
                    continue;
                }

                Approximation result = a.Pow(exponent);
                var images = new List<BigRational>();
                foreach (BigRational x in PointsOf(a))
                {
                    images.Add(BigRational.Pow(x, exponent));
                }

                if (a.Contains(BigRational.Zero) && exponent > 0)
                {
                    images.Add(BigRational.Zero);
                }

                Assert.True(images.Contains(result.Lower), Inv($"Lower {result.Lower} of {a.Lower}..{a.Upper} to the {exponent} is not attained."));
                Assert.True(images.Contains(result.Upper), Inv($"Upper {result.Upper} of {a.Lower}..{a.Upper} to the {exponent} is not attained."));
            }
        }
    }

    // ---------- division and the zero-in-enclosure rule ----------

    [Fact]
    public void Divide_ByAnEnclosureContainingZero_Throws_EvenWhenValueIsNonZero()
    {
        Approximation dividend = Approximation.Exact(BigRational.One);
        Approximation divisor = Approximation.Create(Ratio(1, 10), BigRational.One);

        Assert.False(divisor.Value.IsZero);
        Assert.Throws<DivideByZeroException>(() => dividend / divisor);
    }

    [Fact]
    public void Divide_ByAnEnclosureTouchingZeroAtAnEndpoint_Throws()
    {
        Approximation dividend = Approximation.Exact(BigRational.One);

        Assert.Throws<DivideByZeroException>(() => dividend / Approximation.Create(Ratio(3, 1), Ratio(3, 1)));
        Assert.Throws<DivideByZeroException>(() => dividend / Approximation.Create(Ratio(-1, 7), Ratio(1, 7)));
    }

    [Fact]
    public void Divide_ByExactZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(
            () => Approximation.Exact(BigRational.One) / Approximation.Exact(BigRational.Zero));
    }

    [Fact]
    public void Divide_ThrowsExactlyWhenTheDivisorDoesNotExcludeZero()
    {
        foreach (Approximation b in Enclosures())
        {
            Approximation dividend = Approximation.Exact(BigRational.One);

            if (b.ExcludesZero)
            {
                Approximation unused = dividend / b;
                Assert.True(unused.MaxError.Sign >= 0);
            }
            else
            {
                Assert.Throws<DivideByZeroException>(() => dividend / b);
            }
        }
    }

    [Fact]
    public void Divide_MatchesTheSpecifiedPropagation()
    {
        // [1, 3] / [3, 5]: alpha = 1, beta = 1, a = 2, b = 4.
        // (|b|*alpha + |a|*beta) / ((|b| - beta)*|b|) = (4 + 2) / (3 * 4) = 1/2.
        Approximation result =
            Approximation.Create(Ratio(2, 1), BigRational.One) / Approximation.Create(Ratio(4, 1), BigRational.One);

        Assert.Equal(Ratio(1, 2), result.Value);
        Assert.Equal(Ratio(1, 2), result.MaxError);
    }

    // ---------- Pow ----------

    [Fact]
    public void Pow_WithEvenExponentOverAnIntervalStraddlingZero_HasMinimumZero()
    {
        Approximation straddling = Approximation.Create(BigRational.Zero, Ratio(2, 1));
        Approximation squared = straddling.Pow(2);

        Assert.Equal(BigRational.Zero, squared.Lower);
        Assert.Equal(Ratio(4, 1), squared.Upper);
        Assert.Equal(Ratio(2, 1), squared.Value);
        Assert.Equal(Ratio(2, 1), squared.MaxError);
    }

    [Fact]
    public void Pow_WithEvenExponentOverAnOffCentreStraddle_HasMinimumZero()
    {
        // [-9/10, 11/10] squared has image [0, 121/100]; the signed endpoints alone would give a
        // minimum of 81/100 and would exclude the true minimum.
        Approximation squared = Approximation.Create(Ratio(1, 10), BigRational.One).Pow(2);

        Assert.Equal(BigRational.Zero, squared.Lower);
        Assert.Equal(Ratio(121, 100), squared.Upper);
    }

    [Fact]
    public void Pow_WithOddExponentOverAnIntervalStraddlingZero_StaysMonotonic()
    {
        Approximation cubed = Approximation.Create(BigRational.Zero, Ratio(2, 1)).Pow(3);

        Assert.Equal(Ratio(-8, 1), cubed.Lower);
        Assert.Equal(Ratio(8, 1), cubed.Upper);
    }

    [Fact]
    public void Pow_WithZeroExponent_IsExactlyOne_ForEveryEnclosure()
    {
        foreach (Approximation a in Enclosures())
        {
            Approximation result = a.Pow(0);

            Assert.True(result.IsExact);
            Assert.Equal(BigRational.One, result.Value);
        }
    }

    [Fact]
    public void Pow_WithZeroExponent_IsOneEvenForExactZero()
    {
        Assert.Equal(Approximation.Exact(BigRational.One), Approximation.Exact(BigRational.Zero).Pow(0));
    }

    [Fact]
    public void Pow_WithExponentOne_IsTheIdentity()
    {
        foreach (Approximation a in Enclosures())
        {
            Assert.Equal(a, a.Pow(1));
        }
    }

    [Fact]
    public void Pow_WithNegativeExponent_OverAPositiveInterval_IsTheExactImage()
    {
        // [3/2, 5/2] to the -1 has image [2/5, 2/3], so 8/15 +/- 2/15.
        Approximation result = Approximation.Create(Ratio(2, 1), Ratio(1, 2)).Pow(-1);

        Assert.Equal(Ratio(8, 15), result.Value);
        Assert.Equal(Ratio(2, 15), result.MaxError);
        Assert.Equal(Ratio(2, 5), result.Lower);
        Assert.Equal(Ratio(2, 3), result.Upper);
    }

    [Fact]
    public void Pow_WithNegativeEvenExponent_OverANegativeInterval_IsTheExactImage()
    {
        // [-5/2, -3/2] to the -2 has image [4/25, 4/9].
        Approximation result = Approximation.Create(Ratio(-2, 1), Ratio(1, 2)).Pow(-2);

        Assert.Equal(Ratio(4, 25), result.Lower);
        Assert.Equal(Ratio(4, 9), result.Upper);
    }

    [Fact]
    public void Pow_WithNegativeOddExponent_OverANegativeInterval_IsTheExactImage()
    {
        // [-5/2, -3/2] to the -3 has image [-8/27, -8/125]; both endpoints are negative and the
        // map reverses their order.
        Approximation result = Approximation.Create(Ratio(-2, 1), Ratio(1, 2)).Pow(-3);

        Assert.Equal(Ratio(-8, 27), result.Lower);
        Assert.Equal(Ratio(-8, 125), result.Upper);
    }

    [Fact]
    public void Pow_WithNegativeExponent_OverAnEnclosureContainingZero_Throws()
    {
        // A negative exponent is a reciprocal, so it obeys the division rule: the result is
        // unbounded on an interval that reaches zero, even when Value is non-zero.
        Approximation straddling = Approximation.Create(Ratio(1, 10), BigRational.One);

        Assert.False(straddling.Value.IsZero);
        Assert.Throws<DivideByZeroException>(() => straddling.Pow(-1));
        Assert.Throws<DivideByZeroException>(() => straddling.Pow(-2));
    }

    [Fact]
    public void Pow_WithNegativeExponent_OverAnEnclosureTouchingZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => Approximation.Create(Ratio(3, 1), Ratio(3, 1)).Pow(-1));
        Assert.Throws<DivideByZeroException>(() => Approximation.Exact(BigRational.Zero).Pow(-1));
    }

    [Fact]
    public void Pow_ThrowsForNegativeExponentsExactlyWhenDivisionWould()
    {
        foreach (Approximation a in Enclosures())
        {
            bool powThrew = false;
            try
            {
                _ = a.Pow(-1);
            }
            catch (DivideByZeroException)
            {
                powThrew = true;
            }

            bool divideThrew = false;
            try
            {
                _ = Approximation.Exact(BigRational.One) / a;
            }
            catch (DivideByZeroException)
            {
                divideThrew = true;
            }

            Assert.Equal(divideThrew, powThrew);
        }
    }

    [Fact]
    public void Pow_ReCentres_SoValueIsNotGenerallyTheValueRaisedToThePower()
    {
        Approximation squared = Approximation.Create(BigRational.Zero, BigRational.One).Pow(2);

        Assert.Equal(Ratio(1, 2), squared.Value);
        Assert.Equal(Ratio(1, 2), squared.MaxError);
    }

    [Fact]
    public void Pow_IsTighterThanMultiplyingAnEnclosureByItself()
    {
        // Interval arithmetic's dependency problem: a * a treats its operands as independent
        // unknowns, so it admits negatives that no square can take. Pow does not.
        Approximation a = Approximation.Create(BigRational.Zero, BigRational.One);

        Approximation squared = a.Pow(2);
        Approximation multiplied = a * a;

        Assert.Equal(BigRational.Zero, squared.Lower);
        Assert.Equal(BigRational.One, squared.Upper);

        Assert.Equal(BigRational.MinusOne, multiplied.Lower);
        Assert.Equal(BigRational.One, multiplied.Upper);

        Assert.True(multiplied.Contains(BigRational.MinusOne));
        Assert.False(squared.Contains(BigRational.MinusOne));
        Assert.True(squared.MaxError < multiplied.MaxError);
    }

    // ---------- the invariant every operation must preserve ----------

    [Fact]
    public void EveryOperation_LeavesTheErrorBoundNonNegative()
    {
        foreach (Approximation a in Enclosures())
        {
            foreach (Approximation b in Enclosures())
            {
                AssertBoundIsWellFormed(a + b);
                AssertBoundIsWellFormed(a - b);
                AssertBoundIsWellFormed(a * b);

                if (b.ExcludesZero)
                {
                    AssertBoundIsWellFormed(a / b);
                }
            }

            AssertBoundIsWellFormed(a.Coarsen());
        }
    }

    private static void AssertBoundIsWellFormed(Approximation result)
    {
        Assert.True(result.MaxError.Sign >= 0, Inv($"MaxError went negative: {result.MaxError}."));
        Assert.True(result.Lower <= result.Upper, Inv($"Endpoints inverted: [{result.Lower}, {result.Upper}]."));
    }
}
