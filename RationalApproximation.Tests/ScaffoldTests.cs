using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

public class ScaffoldTests
{
    [Fact]
    public void OneHalf_IsTheReducedFractionOneOverTwo()
    {
        BigRational half = Scaffold.OneHalf;

        Assert.Equal(BigInteger.One, half.Numerator);
        Assert.Equal(new BigInteger(2), half.Denominator);
    }

    [Fact]
    public void OneHalf_AddedToItself_IsExactlyOne()
    {
        Assert.Equal(BigRational.One, Scaffold.OneHalf + Scaffold.OneHalf);
    }
}
