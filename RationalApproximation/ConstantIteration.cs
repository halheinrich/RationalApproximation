namespace HalHeinrich.Numerics;

/// <summary>
/// One iteration of a <see cref="ConstantRun"/>: the target it was driven to, the refinement depth
/// that took, the enclosure that resulted, and the search run against it.
/// </summary>
/// <remarks>
/// <para>
/// A column of the run's <see cref="TrendMatrix"/>, with the bookkeeping the matrix deliberately
/// does not carry. <see cref="TrendIteration"/> holds an enclosure and its candidates because that
/// is all a trend is read from; the depth and the target are what let a reader say <i>how</i> that
/// column was reached, and they belong to the run rather than to the trend.
/// </para>
/// <para>
/// <b>An iteration is not a verdict and does not end a run.</b> No property here reports stability
/// and none should be added: a candidate can hold steady across consecutive iterations and then
/// move on, and where such a plateau falls is not even a property of the constant alone - it moves
/// with where a provider places the truth inside its enclosure. A reflection test bars any member
/// on this type whose name reads as a verdict, and any member returning <see cref="bool"/> at all.
/// </para>
/// </remarks>
public sealed class ConstantIteration
{
    private const string NoCandidatesMessage =
        "The search yielded no candidates, so there is no simplest one. An IRationalApproximator " +
        "must end with the first candidate the enclosure contains, so an implementation that " +
        "yields nothing is defective.";

    internal ConstantIteration(
        BigRational targetError,
        int step,
        Approximation enclosure,
        TrendIteration trend)
    {
        TargetError = targetError;
        Step = step;
        Enclosure = enclosure;
        Trend = trend;
    }

    /// <summary>Gets the target error this iteration was driven to.</summary>
    /// <remarks>
    /// The realised <c>Enclosure.MaxError</c> is at or below this. Nothing here coarsens, so how
    /// far below is the provider's business and not this type's.
    /// </remarks>
    public BigRational TargetError { get; }

    /// <summary>Gets the provider's zero-based refinement index at this iteration.</summary>
    /// <remarks>
    /// The depth this iteration reached, and - read across a run - the evidence that refinement is
    /// incremental: the last iteration's step is the total number of refinements the whole run
    /// pulled, not the number it pulled for that iteration alone.
    /// </remarks>
    public int Step { get; }

    /// <summary>Gets the enclosure this iteration computed.</summary>
    public Approximation Enclosure { get; }

    /// <summary>Gets this iteration's contribution to the run's <see cref="TrendMatrix"/>.</summary>
    public TrendIteration Trend { get; }

    /// <summary>Gets every candidate the search yielded, in the order it yielded them.</summary>
    /// <remarks>
    /// All of them, not just the terminating one. Each is a strict improvement on its predecessor,
    /// and the early low-height ones are exactly the rows a plateau is read from across the whole
    /// run - a matrix row is dense, so a candidate first surfaced late still carries a distance for
    /// every earlier column.
    /// </remarks>
    public IReadOnlyList<RationalCandidate> Candidates => Trend.Candidates;

    /// <summary>
    /// Gets the simplest rational this iteration's evidence permits: the last candidate the search
    /// yielded, which is the first one the enclosure contains.
    /// </summary>
    /// <exception cref="InvalidOperationException">The search yielded no candidates at all.</exception>
    /// <remarks>
    /// This is the least-denominator rational the evidence permits unconditionally, and the
    /// least-height one whenever the enclosure's <see cref="Approximation.MaxError"/> is below
    /// <c>1/2</c> - a condition this bench's enclosures are nowhere near violating, but one worth
    /// stating rather than relying on silently.
    /// </remarks>
    public RationalCandidate Simplest =>
        Candidates.Count > 0 ? Candidates[^1] : throw new InvalidOperationException(NoCandidatesMessage);
}
