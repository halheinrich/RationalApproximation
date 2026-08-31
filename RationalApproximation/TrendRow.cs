using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// One candidate's row of a <see cref="TrendMatrix"/>: its exact distance to the ratio at every
/// iteration of the run.
/// </summary>
/// <remarks>
/// <para>
/// The row is <b>dense</b>. A candidate first surfaced at iteration five still has cells for
/// iterations zero to four, because the distance to an earlier ratio is computable from the
/// candidate and that ratio alone. A sparse row would hide exactly the history the trend is read
/// from.
/// </para>
/// <para>
/// The row reports distances and nothing else. It does not say whether they are falling, whether
/// they have settled, or what that would mean - see <see cref="TrendMatrix"/> for why that
/// omission is the design and not an oversight.
/// </para>
/// </remarks>
public sealed class TrendRow
{
    private readonly BigRational[] distances;

    internal TrendRow(BigRational candidate, BigInteger height, int firstSeenAt, BigRational[] distances)
    {
        Candidate = candidate;
        Height = height;
        FirstSeenAt = firstSeenAt;
        this.distances = distances;
    }

    /// <summary>Gets the candidate rational this row follows.</summary>
    public BigRational Candidate { get; }

    /// <summary>Gets the candidate's naive height, as a measure of how simple it is.</summary>
    public BigInteger Height { get; }

    /// <summary>Gets the index of the earliest iteration that contributed this candidate.</summary>
    /// <remarks>
    /// A record of the run's history, not of the row's validity. The cells before this index are
    /// as real as the ones after it; they were simply computed rather than discovered.
    /// </remarks>
    public int FirstSeenAt { get; }

    /// <summary>
    /// Gets the exact distance from this candidate to each iteration's ratio, one entry per
    /// iteration, in iteration order.
    /// </summary>
    /// <remarks>
    /// Cell <c>k</c> is the exact rational <c>|candidate - x_k|</c>, where <c>x_k</c> is iteration
    /// <c>k</c>'s <see cref="Approximation.Value"/>. Exact, never rounded, never a decimal.
    /// </remarks>
    public IReadOnlyList<BigRational> Distances => distances;
}
