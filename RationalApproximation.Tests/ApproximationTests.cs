using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Construction, the state invariants, the decidable predicates, and <c>Coarsen</c>.
/// Arithmetic and its bound propagation live in <see cref="ApproximationArithmeticTests"/>.
/// </summary>
public class ApproximationTests
{
    // ---------- construction ----------

    [Fact]
    public void Create_RejectsNegativeMaxError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Approximation.Create(BigRational.One, Ratio(-1, 1000000)));
    }

    [Fact]
    public void Create_RejectsNegativeMaxError_EvenWhenTiny()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Approximation.Create(BigRational.Zero, Ratio(-1, int.MaxValue)));
    }

    [Fact]
    public void Create_AcceptsZeroMaxError_AndIsThenExact()
    {
        Approximation a = Approximation.Create(Ratio(22, 7), BigRational.Zero);

        Assert.True(a.IsExact);
        Assert.Equal(Ratio(22, 7), a.Value);
        Assert.Equal(BigRational.Zero, a.MaxError);
    }

    [Fact]
    public void Exact_HasZeroErrorAndCoincidentEndpoints()
    {
        Approximation a = Approximation.Exact(Ratio(-5, 3));

        Assert.True(a.IsExact);
        Assert.Equal(BigRational.Zero, a.MaxError);
        Assert.Equal(Ratio(-5, 3), a.Lower);
        Assert.Equal(Ratio(-5, 3), a.Upper);
    }

    // ---------- the default state ----------

    [Fact]
    public void Default_IsExactZero()
    {
        Approximation a = default;

        Assert.True(a.Value.IsZero);
        Assert.True(a.MaxError.IsZero);
        Assert.True(a.IsExact);
        Assert.True(a.Lower.IsZero);
        Assert.True(a.Upper.IsZero);
    }

    [Fact]
    public void Default_ContainsZeroAndDoesNotExcludeIt()
    {
        Approximation a = default;

        Assert.True(a.Contains(BigRational.Zero));
        Assert.False(a.ExcludesZero);
    }

    [Fact]
    public void Default_EqualsExactZero()
    {
        // BigRational's default state (denominator field zero) is masked to 0/1 by its own
        // properties, so the record struct's generated equality agrees across both spellings.
        Assert.Equal(Approximation.Exact(BigRational.Zero), default(Approximation));
        Assert.Equal(Approximation.Exact(BigRational.Zero).GetHashCode(), default(Approximation).GetHashCode());
    }

    [Fact]
    public void Default_ContainsNothingButZero()
    {
        Approximation a = default;

        Assert.False(a.Contains(BigRational.One));
        Assert.False(a.Contains(Ratio(-1, 1000000000)));
    }

    // ---------- predicates ----------

    [Fact]
    public void LowerAndUpper_AreValuePlusAndMinusMaxError()
    {
        Approximation a = Approximation.Create(Ratio(7, 3), Ratio(1, 6));

        Assert.Equal(Ratio(13, 6), a.Lower);
        Assert.Equal(Ratio(5, 2), a.Upper);
    }

    [Fact]
    public void ExcludesZero_IsTrueOnlyWhenTheBoundIsSmallerThanTheValue()
    {
        Assert.True(Approximation.Create(Ratio(1, 3), Ratio(1, 4)).ExcludesZero);
        Assert.True(Approximation.Create(Ratio(-1, 3), Ratio(1, 4)).ExcludesZero);
        Assert.True(Approximation.Exact(BigRational.One).ExcludesZero);

        // Touching zero at an endpoint is not excluding it.
        Assert.False(Approximation.Create(Ratio(1, 4), Ratio(1, 4)).ExcludesZero);
        Assert.False(Approximation.Create(Ratio(-1, 4), Ratio(1, 4)).ExcludesZero);

        Assert.False(Approximation.Create(Ratio(1, 10), BigRational.One).ExcludesZero);
        Assert.False(Approximation.Exact(BigRational.Zero).ExcludesZero);
    }

    [Fact]
    public void Contains_IncludesBothEndpoints()
    {
        Approximation a = Approximation.Create(Ratio(1, 2), Ratio(1, 4));

        Assert.True(a.Contains(Ratio(1, 4)));
        Assert.True(a.Contains(Ratio(3, 4)));
        Assert.True(a.Contains(Ratio(1, 2)));

        Assert.False(a.Contains(Ratio(1, 5)));
        Assert.False(a.Contains(Ratio(4, 5)));
    }

    [Fact]
    public void ExcludesZero_IsExactlyTheNegationOfContainsZero()
    {
        foreach (Approximation a in Enclosures())
        {
            Assert.Equal(!a.Contains(BigRational.Zero), a.ExcludesZero);
        }
    }

    // ---------- the ordering ruling, encoded ----------

    [Fact]
    public void Approximation_IsDeliberatelyNotComparable()
    {
        // Enclosures are only partially ordered; two that overlap have no defined order.
        // Implementing IComparable would let the compiler accept a sort on something that has no
        // total order, so its absence is a design ruling and not an omission.
        Type type = typeof(Approximation);

        Assert.DoesNotContain(type.GetInterfaces(), i => i == typeof(IComparable));
        Assert.DoesNotContain(type.GetInterfaces(), i => i == typeof(IComparable<Approximation>));
    }

    // ---------- Coarsen ----------

    private static BigRational[] CoarsenInputs() =>
    [
        Ratio(1, 3),
        Ratio(2, 3),
        Ratio(1, 1),
        Ratio(3, 2),
        Ratio(2, 1),
        Ratio(5, 1),
        Ratio(7, 8),
        Ratio(1023, 1024),
        Ratio(1, 2),
        Ratio(1, 1000),
        Ratio(1, 1024),
        Ratio(12345, 67),
        Ratio(1, 1000000000),
        new BigRational(BigInteger.One, BigInteger.One << 40),
        new BigRational(BigInteger.One << 40, BigInteger.One),
        new BigRational((BigInteger.One << 40) + 1, BigInteger.One),
    ];

    [Fact]
    public void Coarsen_NeverNarrowsTheBound()
    {
        foreach (BigRational error in CoarsenInputs())
        {
            Approximation a = Approximation.Create(Ratio(3, 7), error);
            Approximation c = a.Coarsen();

            Assert.True(c.MaxError >= a.MaxError, Inv($"Coarsen narrowed {error} to {c.MaxError}."));
        }
    }

    [Fact]
    public void Coarsen_YieldsAPowerOfTwo()
    {
        foreach (BigRational error in CoarsenInputs())
        {
            BigRational coarsened = Approximation.Create(BigRational.One, error).Coarsen().MaxError;

            Assert.True(IsPowerOfTwo(coarsened), Inv($"Coarsen of {error} gave {coarsened}, not a power of two."));
        }
    }

    [Fact]
    public void Coarsen_YieldsTheNextPowerOfTwo_NotMerelyAnUpperOne()
    {
        foreach (BigRational error in CoarsenInputs())
        {
            BigRational coarsened = Approximation.Create(BigRational.One, error).Coarsen().MaxError;

            // Minimality: halving the result must drop it strictly below the original, which
            // together with the non-narrowing test pins the exponent exactly.
            BigRational halved = coarsened / Ratio(2, 1);
            Assert.True(halved < error, Inv($"Coarsen of {error} gave {coarsened}, which overshot: {halved} is still an upper bound."));
        }
    }

    [Fact]
    public void Coarsen_IsIdempotent()
    {
        foreach (BigRational error in CoarsenInputs())
        {
            Approximation once = Approximation.Create(Ratio(-9, 4), error).Coarsen();
            Approximation twice = once.Coarsen();

            Assert.Equal(once, twice);
        }
    }

    [Fact]
    public void Coarsen_LeavesAnExactPowerOfTwoAlone()
    {
        foreach (BigRational error in new[] { Ratio(1, 1), Ratio(2, 1), Ratio(1, 2), Ratio(1, 1024), new BigRational(BigInteger.One << 40, BigInteger.One) })
        {
            Approximation a = Approximation.Create(BigRational.One, error);

            Assert.Equal(error, a.Coarsen().MaxError);
        }
    }

    [Fact]
    public void Coarsen_PreservesValueAndOnlyWidensTheInterval()
    {
        foreach (BigRational error in CoarsenInputs())
        {
            Approximation a = Approximation.Create(Ratio(11, 13), error);
            Approximation c = a.Coarsen();

            Assert.Equal(a.Value, c.Value);
            Assert.True(c.Lower <= a.Lower, Inv($"Coarsen raised the lower endpoint for {error}."));
            Assert.True(c.Upper >= a.Upper, Inv($"Coarsen lowered the upper endpoint for {error}."));
        }
    }

    [Fact]
    public void Coarsen_OnAnExactApproximation_ReturnsItUnchanged()
    {
        // Ruled: there is no power of two to round zero up to, and widening an exactly-zero bound
        // would discard a proven exactness rather than unread digits. This is the one case in
        // which the result's MaxError is not a power of two.
        Approximation a = Approximation.Exact(Ratio(22, 7));
        Approximation c = a.Coarsen();

        Assert.Equal(a, c);
        Assert.True(c.IsExact);
        Assert.False(IsPowerOfTwo(c.MaxError));
    }

    [Fact]
    public void Coarsen_OnTheDefaultValue_ReturnsItUnchanged()
    {
        Assert.Equal(default, default(Approximation).Coarsen());
    }

    [Fact]
    public void Coarsen_HandlesAnErrorFarBelowOne()
    {
        // The result is two to a negative power; a log2 ceiling that assumed a non-negative
        // exponent, or an integer ceiling, would return one here and lose forty bits of bound.
        BigRational error = new(BigInteger.One, (BigInteger.One << 40) + 1);
        BigRational expected = new(BigInteger.One, BigInteger.One << 40);

        Assert.Equal(expected, Approximation.Create(BigRational.One, error).Coarsen().MaxError);
    }

    [Fact]
    public void Coarsen_RoundsUpNotToNearest()
    {
        // 9/16 is nearer to 1/2 than to 1. Rounding to nearest would narrow the bound, which is a
        // defect rather than a different choice.
        BigRational error = Ratio(9, 16);

        Assert.Equal(BigRational.One, Approximation.Create(BigRational.One, error).Coarsen().MaxError);
    }
}
