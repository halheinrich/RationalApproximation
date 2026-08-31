namespace HalHeinrich.Numerics;

/// <summary>
/// One iteration of a run: the enclosure of the ratio that iteration computed, and the candidates
/// it surfaced. A column of a <see cref="TrendMatrix"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="RationalCandidate.Value"/> and <see cref="RationalCandidate.Height"/> are read
/// when a matrix is built. A candidate's distances are relative to the single enclosure it was
/// judged against, whereas a matrix row spans every iteration - which is the whole reason the
/// matrix exists as something other than a list of candidates.
/// </para>
/// <para>
/// Because only those two members are read, and both are properties of the rational alone, it does
/// no harm to pass a candidate judged against some other enclosure. That also makes it cheap to
/// watch a rational no search produced - a positive control such as 6, 90 or 945 - by including it
/// once, in any iteration. The matrix fills its row for every column regardless.
/// </para>
/// </remarks>
public sealed class TrendIteration
{
    private readonly RationalCandidate[] candidates;

    private TrendIteration(Approximation ratio, RationalCandidate[] candidates)
    {
        Ratio = ratio;
        this.candidates = candidates;
    }

    /// <summary>Gets the enclosure of the ratio this iteration computed.</summary>
    /// <remarks>
    /// Its <see cref="Approximation.Value"/> is the <c>x_k</c> the row distances are measured
    /// against; its <see cref="Approximation.MaxError"/> is what says how much of an apparent
    /// plateau the evidence actually supports.
    /// </remarks>
    public Approximation Ratio { get; }

    /// <summary>Gets the candidates this iteration contributed to the matrix.</summary>
    public IReadOnlyList<RationalCandidate> Candidates => candidates;

    /// <summary>Records an iteration of a run.</summary>
    /// <param name="ratio">The enclosure of the ratio this iteration computed.</param>
    /// <param name="candidates">
    /// The candidates this iteration contributes. Copied, so a later change to the source has no
    /// effect. May be empty.
    /// </param>
    /// <returns>The iteration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candidates"/> is null.</exception>
    /// <remarks>
    /// Which candidates belong here is the caller's decision and deliberately not this type's. The
    /// natural feed is the terminating candidate of each iteration's search - the simplest
    /// rational that iteration's evidence permitted - but a caller may pass every improvement the
    /// search yielded, or add controls it wants watched. Encoding a policy here would be this
    /// layer deciding which candidates deserve to be looked at.
    /// </remarks>
    public static TrendIteration Of(Approximation ratio, IEnumerable<RationalCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return new TrendIteration(ratio, [.. candidates]);
    }
}
