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
/// candidate is the constant - so a row falling towards zero is the answer and every other row
/// settles at that candidate's true distance from it. But recurrence is not required and must not
/// be relied on: measured runs have shown a candidate holding steady for two consecutive
/// iterations and then moving on, so any "unchanged for k rounds" rule with k = 2 gives a false
/// positive on cases that have actually been observed.
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
