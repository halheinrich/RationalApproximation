using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// The exact distance from every candidate to the ratio at every iteration of a run: rows are
/// candidates, columns are iterations, cells are the exact <c>|a/b - x_k|</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type reports no verdict, and that is the design.</b> Since
/// <c>|a/b - x_k|</c> tends to <c>|a/b - X|</c>, a row's limit is zero if and only if the
/// candidate is the constant, and every other row settles at that candidate's true distance from
/// it. That is true and it is <b>unobservable</b>. At any finite iteration a row falling towards a
/// very small number is indistinguishable from one falling to zero - a measured near-miss sat four
/// orders of magnitude below the true answer's row before it died - so reading the limit off the
/// rows is an inference the data does not support, however the rule is phrased. Recurrence fails
/// on candidates that held steady for two consecutive iterations and then moved on; a plateau
/// stops being flat when the height cap moves.
/// </para>
/// <para>
/// <b>What decides instead is <see cref="SurvivorSearch"/>.</b> A candidate outside an enclosure
/// of the unknown is not the unknown, permanently, so refutation is a proof where a falling row is
/// a reading. This matrix stays because it is how a reader sees what the search is doing, and
/// where a near-miss becomes visible <i>as</i> a near-miss; it is no longer what decides. See
/// <c>SPEC-rational-ratio.md</c> § 2, "Why survivors rather than a trend".
/// </para>
/// <para>
/// There is therefore deliberately no <c>IsConverged</c>, no <c>Answer</c>, and no
/// <c>HasPlateaued</c>. Each of those would be that rejected stopping rule wearing a property
/// name, and the first caller to find one would read it as a verdict. This type presents exact
/// data; the reading is the caller's, and it is made over the whole matrix after a run has been
/// driven to a fixed error target - never as a reason to stop early.
/// </para>
/// <para>
/// Cells are exact <see cref="BigRational"/> distances. Presentation may format them for display;
/// nothing here does.
/// </para>
/// </remarks>
public sealed class TrendMatrix
{
    private readonly Approximation[] ratios;
    private readonly TrendRow[] rows;

    private TrendMatrix(Approximation[] ratios, TrendRow[] rows)
    {
        this.ratios = ratios;
        this.rows = rows;
    }

    /// <summary>Gets each iteration's ratio enclosure, in iteration order. The columns.</summary>
    /// <remarks>
    /// The full enclosure is kept rather than just the value the distances are measured against,
    /// because a column's <see cref="Approximation.MaxError"/> is what tells a reader how much of
    /// an apparent plateau the evidence supports.
    /// </remarks>
    public IReadOnlyList<Approximation> Ratios => ratios;

    /// <summary>Gets one row per distinct candidate, ordered by height and then by value.</summary>
    /// <remarks>
    /// The order is an index, not a ranking. It is chosen so that a candidate lands in the same
    /// position whichever iteration surfaced it and whether or not the run is later extended,
    /// which is what makes two runs comparable side by side;
    /// <see cref="TrendRow.FirstSeenAt"/> carries the discovery history that a
    /// first-appearance ordering would otherwise encode.
    /// </remarks>
    public IReadOnlyList<TrendRow> Rows => rows;

    /// <summary>Builds the matrix for a completed run.</summary>
    /// <param name="iterations">The run's iterations, in order.</param>
    /// <returns>The matrix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="iterations"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Takes the whole run at once rather than accumulating it. A builder that could be read while
    /// the run was still going would invite exactly the early exit on apparent stability that this
    /// design rejects; a snapshot of a finished run cannot be.
    /// </para>
    /// <para>
    /// A candidate contributed by more than one iteration gets a single row, recording the earliest
    /// iteration that contributed it. A run with no iterations yields an empty matrix, which is
    /// honestly empty rather than an error.
    /// </para>
    /// </remarks>
    public static TrendMatrix Build(IEnumerable<TrendIteration> iterations)
    {
        ArgumentNullException.ThrowIfNull(iterations);

        TrendIteration[] run = [.. iterations];
        var ratios = new Approximation[run.Length];
        for (int column = 0; column < run.Length; column++)
        {
            ratios[column] = run[column].Ratio;
        }

        // Distinct candidates, each remembering the earliest iteration that contributed it.
        var firstSeenAt = new Dictionary<BigRational, int>();
        var heights = new Dictionary<BigRational, BigInteger>();
        for (int column = 0; column < run.Length; column++)
        {
            foreach (RationalCandidate candidate in run[column].Candidates)
            {
                if (firstSeenAt.TryAdd(candidate.Value, column))
                {
                    heights[candidate.Value] = candidate.Height;
                }
            }
        }

        var built = new List<TrendRow>(firstSeenAt.Count);
        foreach (KeyValuePair<BigRational, int> entry in firstSeenAt)
        {
            BigRational candidate = entry.Key;

            // Dense: every column, not just the ones at or after FirstSeenAt.
            var distances = new BigRational[ratios.Length];
            for (int column = 0; column < ratios.Length; column++)
            {
                distances[column] = BigRational.Abs(candidate - ratios[column].Value);
            }

            built.Add(new TrendRow(candidate, heights[candidate], entry.Value, distances));
        }

        built.Sort(CompareRows);

        return new TrendMatrix(ratios, [.. built]);
    }

    private static int CompareRows(TrendRow left, TrendRow right)
    {
        int byHeight = left.Height.CompareTo(right.Height);
        return byHeight != 0 ? byHeight : left.Candidate.CompareTo(right.Candidate);
    }
}
