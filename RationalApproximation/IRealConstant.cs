namespace HalHeinrich.Numerics;

/// <summary>
/// A real constant that can be computed to any requested accuracy, supplying both a value and a
/// proven bound on that value's distance from the truth.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are <b>rational-valued and strictly improving</b>. The formula is an
/// implementation detail; the bound is not. A bound is proven analytically, never measured
/// empirically and never estimated from observed convergence.
/// </para>
/// <para>
/// Nothing in the type system enforces the obligations below. They are contract, and an
/// implementation that breaks one is defective even though it compiles: the defaulted members
/// here are built on them, and so is every consumer.
/// </para>
/// <para>
/// <see cref="StepFor"/> and <see cref="ApproximateTo"/> are default interface members, so they
/// are reachable only through an <see cref="IRealConstant"/>-typed reference and not from a
/// concrete provider's own surface. A caller of a provider that has <i>not</i> re-declared them
/// must therefore hold the interface. An implementation that wants them on its own type may
/// re-declare them, and should then delegate rather than reimplement - after which the concrete
/// type is the one to hold, because an interface-typed reference to it has stopped being merely
/// unnecessary and is flagged, and only a member left undeclared still needs a cast. The two
/// halves of that advice apply to disjoint types; following the wrong half does not compile.
/// </para>
/// </remarks>
public interface IRealConstant
{
    private const string TargetMustBePositiveMessage =
        "The target error must be strictly positive. A bound tends to zero without reaching it, " +
        "so a target of zero or less would never be met.";

    private const string BoundDidNotConvergeMessage =
        "ErrorBoundAt did not reach the target within int.MaxValue steps. The implementation " +
        "violates the tending-to-zero obligation of IRealConstant.";

    private const string RefinementsEndedMessage =
        "Refinements() ended. The implementation violates the endlessness obligation of " +
        "IRealConstant.";

    /// <summary>
    /// Gets the proven upper bound on the error of the refinement at the given step, without
    /// computing that refinement.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns>An upper bound on the step's distance from the true value.</returns>
    /// <remarks>
    /// <para>
    /// The obligations on an implementation are:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Pure</b> - the same step always gives the same answer, with no observable side effect.</description></item>
    /// <item><description><b>Non-increasing</b> - a later step never has a larger bound. It need not decrease strictly; a bound that is flat over a run of steps is allowed.</description></item>
    /// <item><description><b>Tending to zero</b> - for any positive target there exists a step whose bound is at or below it.</description></item>
    /// <item><description><b>Computable without doing the step's work</b> - evaluating this must not cost what evaluating the refinement would.</description></item>
    /// </list>
    /// <para>
    /// The last of those is the point of the whole member, and it is what makes
    /// <see cref="StepFor"/> honest: a run can be <i>planned</i> before it is paid for. An
    /// implementation that computes the refinement in order to report its bound satisfies the
    /// signature and defeats the purpose.
    /// </para>
    /// <para>
    /// This is an upper bound on the step's error, not the step's error. The refinement's own
    /// <see cref="Approximation.MaxError"/> may be smaller, and consumers that care about the
    /// realised accuracy should read that instead.
    /// </para>
    /// </remarks>
    public BigRational ErrorBoundAt(int step);

    /// <summary>Gets the endless sequence of successively better approximations to this constant.</summary>
    /// <returns>A lazy, endless sequence of enclosures.</returns>
    /// <remarks>
    /// <para>
    /// The obligations on an implementation are:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Lazy</b> - a refinement is computed when it is pulled, not before.</description></item>
    /// <item><description><b>Endless</b> - the sequence never terminates. A consumer takes a finite prefix; so must a test.</description></item>
    /// <item><description><b>Strictly improving</b> - each element's <see cref="Approximation.MaxError"/> is strictly less than its predecessor's.</description></item>
    /// <item><description><b>Incremental</b> - each refinement builds on the last rather than recomputing from scratch.</description></item>
    /// </list>
    /// <para>
    /// Every element encloses the true value, so the intersection of any prefix does too.
    /// </para>
    /// </remarks>
    public IEnumerable<Approximation> Refinements();

    /// <summary>
    /// Gets the first step whose error bound is at or below the given target.
    /// </summary>
    /// <param name="targetError">The error to reach. Must be strictly positive.</param>
    /// <returns>
    /// The least step index <c>n</c> with <c>ErrorBoundAt(n) &lt;= targetError</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetError"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// The bound had not reached the target by <see cref="int.MaxValue"/> steps, so the
    /// implementation is not tending to zero.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Implemented from <see cref="ErrorBoundAt"/> alone, so it costs no refinements at all: this
    /// answers "how deep would I have to go" without going there.
    /// </para>
    /// <para>
    /// The search doubles to bracket the answer and then bisects, taking a logarithmic number of
    /// bound evaluations rather than the linear scan the answer's size would otherwise cost. A
    /// non-increasing bound makes "is this step good enough" monotone in the step, which is what
    /// bisection needs; a bound that is flat over a run of steps returns the first step of the
    /// run, not an arbitrary member of it.
    /// </para>
    /// </remarks>
    public int StepFor(BigRational targetError)
    {
        if (targetError.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetError), TargetMustBePositiveMessage);
        }

        if (ErrorBoundAt(0) <= targetError)
        {
            return 0;
        }

        // Bracket: low never qualifies, high does once the loop ends.
        int low = 0;
        int high = 1;
        while (ErrorBoundAt(high) > targetError)
        {
            low = high;

            if (high == int.MaxValue)
            {
                throw new InvalidOperationException(BoundDidNotConvergeMessage);
            }

            high = high <= int.MaxValue / 2 ? high * 2 : int.MaxValue;
        }

        // Bisect the bracket down to adjacency; high is then the least qualifying step.
        while (high - low > 1)
        {
            int middle = low + ((high - low) / 2);
            if (ErrorBoundAt(middle) <= targetError)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return high;
    }

    /// <summary>
    /// Gets the first refinement whose error is at or below the given target.
    /// </summary>
    /// <param name="targetError">The error to reach. Must be strictly positive.</param>
    /// <returns>An enclosure whose <see cref="Approximation.MaxError"/> is at most <paramref name="targetError"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetError"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Refinements"/> ended, so the implementation is not endless.
    /// </exception>
    /// <remarks>
    /// Implemented from <see cref="Refinements"/>, and it stops on the refinement's realised
    /// <see cref="Approximation.MaxError"/> rather than on <see cref="ErrorBoundAt"/>. Since the
    /// bound is only an upper bound on the step's error, a refinement can meet the target before
    /// its step's bound does; stopping on the realised error therefore never does more work and
    /// sometimes does less.
    /// </remarks>
    public Approximation ApproximateTo(BigRational targetError)
    {
        if (targetError.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetError), TargetMustBePositiveMessage);
        }

        foreach (Approximation refinement in Refinements())
        {
            if (refinement.MaxError <= targetError)
            {
                return refinement;
            }
        }

        throw new InvalidOperationException(RefinementsEndedMessage);
    }
}
