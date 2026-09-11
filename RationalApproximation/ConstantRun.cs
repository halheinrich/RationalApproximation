namespace HalHeinrich.Numerics;

/// <summary>
/// A complete run against a single real constant: enclose, search for rationals, iterate at
/// successively tighter targets, and assemble the trend matrix.
/// </summary>
/// <remarks>
/// <para>
/// The composition, and nothing else. Every contract wired here is already this layer's -
/// <see cref="IRealConstant"/>, <see cref="IRationalApproximator"/>, <see cref="Approximation"/>
/// and <see cref="TrendMatrix"/>. This type introduces no enclosure, no search and no constant of
/// its own.
/// </para>
/// <para>
/// <b>One constant, no divisor.</b> This is not a degenerate case of a ratio run and must not be
/// built as one. A ratio's distinctive content is the error-share decision between two operands -
/// which of them to refine next, given that the target is on the quotient and neither operand can
/// answer that alone. With one constant there is only one thing to advance, so the decision does
/// not exist. Passing an identity element to a two-operand runner to reach a one-operand question
/// would manufacture a divisor the problem does not have, and an exact divisor cannot be a
/// conforming <see cref="IRealConstant"/> anyway.
/// </para>
/// <para>
/// <b>The run does not stop early, and there is no property here that says it should.</b> A
/// candidate holding steady across iterations is not evidence; where such a plateau falls is not
/// even a property of the constant alone, since it moves with where the provider places the truth
/// inside its enclosure. The run is driven to the fixed sequence of targets it was given and the
/// whole matrix is read afterwards.
/// </para>
/// <para>
/// <b>What the answer means, and what decides it.</b> A row of the matrix falling towards zero
/// would mean the candidate is the constant only in the limit, and no finite run reaches a limit;
/// at any iteration a row falling towards a very small number is indistinguishable from one
/// falling to zero. So the rows are not what decides. <see cref="SurvivorSearch"/> is: run over
/// this run's enclosures - <see cref="ConstantIteration.Enclosure"/> from each of
/// <see cref="Iterations"/> - under a denominator bound fixed in advance, it reports the rationals
/// no enclosure excludes. A candidate outside any one enclosure is not the constant, permanently,
/// so that set only shrinks and an empty tail to it is the refutation. The matrix is kept for what
/// it is good at: showing a reader what the search is doing, and showing a near-miss <i>as</i> a
/// near-miss.
/// </para>
/// <para>
/// <b>A bound reported from a run must name the searcher that produced it</b>, because the axis
/// follows the searcher and the two are not interchangeable.
/// <see cref="Execute"/> takes the searcher as an argument and defaults it to
/// <see cref="DenominatorSweep"/>, which enumerates denominators and so bounds them - for any
/// numerator, which is the stronger claim. <see cref="HeightSweep"/> above one enumerates
/// numerators instead and so bounds <i>height</i>, saying nothing about denominators;
/// <see cref="HeightSweep.SearchesNumerators"/> is what tells a reporting site which it got. This
/// paragraph read "the sweep" when there was one searcher. With two, a bound that does not name
/// its searcher is not checkable - see <c>SPEC-rational-ratio.md</c> § 1.
/// </para>
/// <para>
/// <b>A near miss is the case all of this exists to refuse.</b> A constant sitting just outside a
/// low-height rational produces a row that falls at every column, exactly as a genuine find would,
/// for as long as the target error stays well above the constant's distance from that rational.
/// The row levels off only near that distance, and a schedule that stops short of it never sees
/// the floor. Read as a trend that is indistinguishable from a find at every reachable precision;
/// read as membership it is settled the moment one enclosure excludes the candidate, and it never
/// comes back.
/// </para>
/// <para>
/// <b>What a run costs is not a function of its targets.</b> The refinement half is plannable -
/// <see cref="IRealConstant.StepFor"/> answers it without pulling a single refinement. The search
/// half is not, and not for want of a helper here: the depth
/// <see cref="IRationalApproximator.Search"/> reaches depends on the continued-fraction structure
/// of the unknown, which is what an investigation does not know. <c>SPEC-rational-ratio.md</c>
/// § 2, "What a search costs", owns the two measured regimes and how far apart they fall; they are
/// not restated here, and the expensive one is exactly the near-miss shape this bench exists to
/// refuse. <b>So a caller must never assume a schedule is affordable because a law says so.</b>
/// </para>
/// </remarks>
public sealed class ConstantRun
{
    private const string TargetsMustDecreaseMessage =
        "Target errors must be strictly decreasing. A repeated target produces a duplicate column " +
        "that is not fresh evidence, and a larger one cannot be honoured at all, since a bound " +
        "already proven tighter is not un-proven.";

    private const string LastTargetMustBePositiveMessage =
        "The final target error must be strictly positive. An error bound tends to zero without " +
        "reaching it, so a target of zero or less would never be met.";

    private const string RefinementsEndedMessage =
        "Refinements() ended. The implementation violates the endlessness obligation of " +
        "IRealConstant.";

    private readonly ConstantIteration[] iterations;

    private ConstantRun(ConstantIteration[] iterations, TrendMatrix matrix)
    {
        this.iterations = iterations;
        Matrix = matrix;
    }

    /// <summary>Gets the run's iterations, in order. The matrix's columns, with their bookkeeping.</summary>
    public IReadOnlyList<ConstantIteration> Iterations => iterations;

    /// <summary>Gets the trend matrix over the whole run: rows are candidates, columns iterations.</summary>
    /// <remarks>
    /// Built from every candidate every iteration surfaced. A caller wanting to watch a rational no
    /// search produced - a control it wants a row for - builds its own matrix from
    /// <see cref="Iterations"/>, adding that candidate to any one iteration's contribution; the
    /// matrix fills its row for every column regardless.
    /// </remarks>
    public TrendMatrix Matrix { get; }

    /// <summary>Runs the pipeline to each target error in turn.</summary>
    /// <param name="constant">The constant to enclose and search against.</param>
    /// <param name="targetErrors">
    /// The target error for each iteration, strictly decreasing, the last strictly positive. May be
    /// empty, which is an honestly empty run rather than an error.
    /// </param>
    /// <param name="approximator">
    /// The search to run against each iteration's enclosure. Defaults to
    /// <see cref="DenominatorSweep"/>, the reference implementation.
    /// </param>
    /// <returns>The completed run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="constant"/> or <paramref name="targetErrors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="targetErrors"/> is not strictly decreasing, or its last element is not positive.</exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="constant"/>'s <see cref="IRealConstant.Refinements"/> ended, so the
    /// implementation is not endless.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The schedule of targets is the caller's, deliberately. How far a run should go, and in how
    /// many columns, is a property of the question being asked rather than of the pipeline; what is
    /// fixed is that the run is driven to a <i>fixed</i> target rather than stopped on what the
    /// output looks like.
    /// </para>
    /// <para>
    /// <b>One refinement sequence is held across the whole schedule and advanced.</b> The cost of
    /// the last iteration is therefore the depth it reaches, not that depth times the number of
    /// iterations. <see cref="IRealConstant.ApproximateTo"/> is deliberately not used: it is
    /// implemented as a fresh <c>foreach</c> over <see cref="IRealConstant.Refinements"/>, so
    /// calling it once per target would restart from step zero every time and make a run cost the
    /// sum of its prefixes - defeating the incremental obligation the interface places on
    /// providers. <see cref="ConstantIteration.Step"/> is what makes that observable: the last
    /// iteration's step is the whole run's refinement count.
    /// </para>
    /// <para>
    /// An empty schedule pulls no refinements at all, so an honestly empty run is also a free one.
    /// </para>
    /// </remarks>
    public static ConstantRun Execute(
        IRealConstant constant,
        IEnumerable<BigRational> targetErrors,
        IRationalApproximator? approximator = null)
    {
        ArgumentNullException.ThrowIfNull(constant);
        ArgumentNullException.ThrowIfNull(targetErrors);

        BigRational[] targets = [.. targetErrors];
        string? complaint = FaultInTargets(targets);
        if (complaint is not null)
        {
            throw new ArgumentException(complaint, nameof(targetErrors));
        }

        IRationalApproximator search = approximator ?? new DenominatorSweep();
        var completed = new ConstantIteration[targets.Length];

        using (IEnumerator<Approximation> refinements = constant.Refinements().GetEnumerator())
        {
            Approximation current = default;
            int step = -1;

            for (int index = 0; index < targets.Length; index++)
            {
                // The step < 0 clause is what pulls step 0; after that the target decides. A
                // target already met by the refinement in hand advances nothing, which is what
                // makes the whole run cost one pass rather than one pass per column.
                while (step < 0 || current.MaxError > targets[index])
                {
                    if (!refinements.MoveNext())
                    {
                        throw new InvalidOperationException(RefinementsEndedMessage);
                    }

                    current = refinements.Current;
                    step++;
                }

                TrendIteration trend = TrendIteration.Of(current, search.Search(current));
                completed[index] = new ConstantIteration(targets[index], step, current, trend);
            }
        }

        TrendMatrix matrix = TrendMatrix.Build(completed.Select(iteration => iteration.Trend));
        return new ConstantRun(completed, matrix);
    }

    // A property of the sequence rather than of any one target: strictly decreasing with a
    // positive last element makes every element positive, and checking it here means an ill-formed
    // schedule fails before any refinement is paid for. Returns the complaint rather than throwing
    // so the caller can name its own parameter.
    private static string? FaultInTargets(BigRational[] targets)
    {
        if (targets.Length == 0)
        {
            return null;
        }

        for (int index = 1; index < targets.Length; index++)
        {
            if (targets[index] >= targets[index - 1])
            {
                return TargetsMustDecreaseMessage;
            }
        }

        return targets[^1].Sign <= 0 ? LastTargetMustBePositiveMessage : null;
    }
}
