using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// A rational proposed as the value of an unknown, together with what a given enclosure of that
/// unknown lets you say about how far away it is.
/// </summary>
/// <remarks>
/// <para>
/// The distance from a candidate to the truth is <b>itself only known to within the
/// enclosure</b>, which is why there are two of them. <see cref="MinDistance"/> and
/// <see cref="MaxDistance"/> are the endpoints of that interval, and reporting one without the
/// other would state a distance the evidence does not support.
/// </para>
/// <para>
/// Only the candidate's value and the enclosure it was judged against are stored. Height, the two
/// distances and <see cref="IsEnclosed"/> are all derived, so they cannot fall out of step with
/// each other or with the enclosure. The default value is the rational zero judged against the
/// exactly-zero enclosure, which is coherent: zero distance, and enclosed.
/// </para>
/// </remarks>
public readonly record struct RationalCandidate
{
    private readonly Approximation _enclosure;

    private RationalCandidate(BigRational value, Approximation enclosure)
    {
        Value = value;
        _enclosure = enclosure;
    }

    /// <summary>Gets the candidate rational.</summary>
    public BigRational Value { get; }

    /// <summary>Gets the naive height of the candidate: the larger of the absolute numerator and the denominator.</summary>
    /// <remarks>
    /// This is a property of the <b>reduced</b> fraction, never of the sweep index that produced
    /// it: a sweep at denominator 2 yielding <c>12/2</c> carries height 6, not 12.
    /// <see cref="BigRational"/> is always in lowest terms with a positive denominator, so
    /// reading the parts here is already reading the reduced form.
    /// </remarks>
    public BigInteger Height => BigInteger.Max(BigInteger.Abs(Value.Numerator), Value.Denominator);

    /// <summary>
    /// Gets the least distance from this candidate to the unknown that the enclosure permits.
    /// </summary>
    /// <remarks>Zero when the candidate lies inside the enclosure, since the unknown may then be the candidate itself.</remarks>
    public BigRational MinDistance
    {
        get
        {
            BigRational lower = _enclosure.Lower;
            BigRational upper = _enclosure.Upper;

            if (Value < lower)
            {
                return lower - Value;
            }

            if (Value > upper)
            {
                return Value - upper;
            }

            return BigRational.Zero;
        }
    }

    /// <summary>
    /// Gets the greatest distance from this candidate to the unknown that the enclosure permits.
    /// </summary>
    public BigRational MaxDistance
    {
        get
        {
            BigRational lower = _enclosure.Lower;
            BigRational upper = _enclosure.Upper;

            if (Value < lower)
            {
                return upper - Value;
            }

            if (Value > upper)
            {
                return Value - lower;
            }

            BigRational toLower = Value - lower;
            BigRational toUpper = upper - Value;
            return toLower >= toUpper ? toLower : toUpper;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the enclosure permits this candidate to be the unknown
    /// exactly. This is the event a search looks for.
    /// </summary>
    public bool IsEnclosed => _enclosure.Contains(Value);

    /// <summary>Judges a rational against an enclosure of the unknown.</summary>
    /// <param name="value">The candidate rational.</param>
    /// <param name="enclosure">The enclosure of the unknown to judge it against.</param>
    /// <returns>The candidate, carrying its height and its distance interval.</returns>
    public static RationalCandidate Against(BigRational value, Approximation enclosure) =>
        new(value, enclosure);
}
