using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Shared fixtures for the bound tests. A bound is tested by trying to violate it: take input
/// enclosures with known exact endpoints, sample the inputs, compute the operation exactly on
/// those samples, and require every result to lie inside the output enclosure. A propagation that
/// is too tight fails such a sweep; one that merely held once does not prove anything.
/// </summary>
internal static class Sampling
{
    /// <summary>
    /// Input enclosures spanning the shapes that change an operation's behaviour: exact, strictly
    /// positive, strictly negative, straddling zero with a non-zero centre, centred on zero,
    /// touching zero from either side, and a wide value with a narrow bound.
    /// </summary>
    public static Approximation[] Enclosures() =>
    [
        Approximation.Exact(BigRational.Zero),
        Approximation.Exact(BigRational.One),
        Approximation.Exact(Ratio(-3, 1)),
        Approximation.Create(Ratio(5, 1), Ratio(1, 2)),
        Approximation.Create(Ratio(-7, 3), Ratio(1, 4)),
        Approximation.Create(Ratio(1, 10), Ratio(1, 1)),
        Approximation.Create(BigRational.Zero, Ratio(2, 1)),
        Approximation.Create(Ratio(3, 1), Ratio(3, 1)),
        Approximation.Create(Ratio(-1, 7), Ratio(1, 7)),
        Approximation.Create(Ratio(1000, 1), Ratio(1, 1000)),
    ];

    /// <summary>
    /// Points of the closed interval: both endpoints, the centre, and interior points at eighths.
    /// Exact rational arithmetic throughout - no floating point, not even to place a sample.
    /// </summary>
    public static BigRational[] PointsOf(Approximation enclosure)
    {
        BigRational lower = enclosure.Lower;
        BigRational width = enclosure.Upper - lower;

        var points = new BigRational[9];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = lower + (width * Ratio(i, points.Length - 1));
        }

        return points;
    }

    /// <summary>Builds an exact rational, keeping the test fixtures free of decimal literals.</summary>
    public static BigRational Ratio(int numerator, int denominator) =>
        new(numerator, denominator);

    /// <summary>
    /// Formats a failure message with the invariant culture, so a diagnostic never depends on the
    /// machine running the suite.
    /// </summary>
    public static string Inv(FormattableString message) => FormattableString.Invariant(message);

    /// <summary>
    /// Finds the first step whose error bound meets a target by scanning from zero, which is the
    /// definition <see cref="IRealConstant.StepFor"/> is checked against.
    /// </summary>
    /// <remarks>
    /// Built from the definition and never from a search, because a search checked against another
    /// search proves only that the two agree. Shared rather than repeated: the expectation for
    /// <see cref="IRealConstant"/>'s bisection and the expectation for
    /// <see cref="AffineConstant.StepFor"/>'s delegation are the same rule, and a rule stated twice
    /// can only be re-diverged.
    /// </remarks>
    public static int ScanForStep(IRealConstant constant, BigRational target)
    {
        for (int step = 0; ; step++)
        {
            if (constant.ErrorBoundAt(step) <= target)
            {
                return step;
            }
        }
    }

    /// <summary>Determines whether a positive rational is an exact power of two, of either sign.</summary>
    public static bool IsPowerOfTwo(BigRational value)
    {
        if (value.Sign <= 0)
        {
            return false;
        }

        BigInteger p = value.Numerator;
        BigInteger q = value.Denominator;
        return (p.IsOne && IsPowerOfTwo(q)) || (q.IsOne && IsPowerOfTwo(p));
    }

    private static bool IsPowerOfTwo(BigInteger value) =>
        value > BigInteger.Zero && (value & (value - BigInteger.One)).IsZero;
}
