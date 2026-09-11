using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// The height-ordered <see cref="IRationalApproximator"/>: enumerate candidates in increasing
/// naive height, taking the closest rational of each height, and keep only the ones that improve.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="DenominatorSweep"/>, and its point is the ordering rather than the
/// answer. The sweep enumerates denominators, so on a target of 6 it proposes <c>6/1</c> at
/// denominator 1 and stops: a step-by-step exhibit of that run shows one candidate and no
/// refutation. This type tries <c>1/1 2/1 3/1 4/1 5/1 6/1</c>, which makes "are we trying other
/// constants" a visible answer rather than an assertion.
/// </para>
/// <para>
/// <b>Height, not numerator - and the axis is chosen from the target.</b> Naive height is
/// <c>max(|p|, q)</c>. A reduced <c>p/q</c> near a target of magnitude above one has
/// <c>|p| &gt; q</c>, so the numerator carries the height; below one the denominator does. This
/// type therefore varies numerators when <c>|Value| &gt; 1</c> and delegates to
/// <see cref="DenominatorSweep"/> otherwise, so that either way it is enumerating heights
/// <c>1, 2, 3, ...</c> A caller wanting the least-height rational an enclosure admits does not
/// have to know which side of one its target sits on to pick a type.
/// </para>
/// <para>
/// A fixed numerator axis would not merely be inconvenient below one, it would stop being height
/// order at all: against a target near <c>0.34</c> the numerators <c>1, 2, 3, ...</c> pair with
/// denominators <c>3, 6, 9, ...</c>, so the heights visited jump by three and all but the first of
/// each family is discarded as unreduced. Choosing the axis is also what removes the only hazard
/// on this side: the numerator axis divides by the target, where <see cref="DenominatorSweep"/>
/// multiplies by an integer and so needs no sign or zero guard. Dividing here can only happen
/// where <c>|Value| &gt; 1</c>, so a target of zero - or one near it - never reaches the division,
/// and falls instead to the axis that was always fine there.
/// </para>
/// <para>
/// <b>In the delegating regime this is not an independent implementation, and a cross-check there
/// proves nothing.</b> Below one the height axis <i>is</i> the denominator axis, so rather than
/// restate that loop this type hands the enclosure to <see cref="DenominatorSweep"/>. Two searches
/// agreeing is the strongest correctness evidence available here, and it is only evidence where
/// the two are actually different code - which is <c>|Value| &gt; 1</c>.
/// </para>
/// <para>
/// <b>This type is never optimised</b>, for the same reason its sibling is not: its only product
/// is trust. It costs a factor of the target's magnitude more than the sweep, and that is not
/// slack to reclaim. Where the enclosure contains at most one integer the two searches end at the
/// same rational, the sweep having reached its denominator and this having reached its numerator,
/// so the ratio of the indices they examine <i>is</i> the target - a target near <c>25.79</c> at a
/// radius of <c>1/100</c> ends at <c>129/5</c>, which is 129 numerators against 5 denominators.
/// With two or more integers enclosed they can part, which is the difference
/// <see cref="Search"/>'s remarks describe: both stop at an integer, the sweep at the one nearest
/// the value and this at the one of least magnitude. Being slower is the price of enumerating the
/// order a reader can follow.
/// </para>
/// <para>
/// The search always terminates. An enclosure's value is a <see cref="BigRational"/> <c>n/d</c> in
/// lowest terms, so at numerator <c>|n|</c> the closest rational of that numerator is the centre
/// itself, which the enclosure contains by definition.
/// </para>
/// </remarks>
public sealed class HeightSweep : IRationalApproximator
{
    private static readonly DenominatorSweep Denominators = new();

    /// <summary>
    /// Determines whether a search over the given enclosure varies numerators rather than
    /// denominators, equivalently whether <c>|Value| &gt; 1</c>.
    /// </summary>
    /// <param name="enclosure">The enclosure to be searched.</param>
    /// <returns>
    /// <see langword="true"/> if the search will enumerate numerators, so that the terminating
    /// candidate's numerator is the bound it proves; <see langword="false"/> if it will enumerate
    /// denominators, so that its denominator is.
    /// </returns>
    /// <remarks>
    /// Exposed because the axis decides what the result <i>means</i>. A completed search proves
    /// that everything below the index it stopped at misses the enclosure, and that index is a
    /// numerator on one side of one and a denominator on the other; a bound reported without
    /// saying which is not a bound a reader can use. Deriving it from <c>|Value| &gt; 1</c> at each
    /// reporting site would be the same rule written in as many places as there are callers.
    /// </remarks>
    public static bool SearchesNumerators(Approximation enclosure) =>
        BigRational.Abs(enclosure.Value) > BigRational.One;

    /// <summary>Searches for rationals the given enclosure permits, simplest first.</summary>
    /// <param name="enclosure">The enclosure of the unknown to search against.</param>
    /// <returns>
    /// A lazy sequence of candidates of increasing height, each strictly closer to the enclosure's
    /// value than the last, ending with the first one the enclosure contains.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Heights run <c>1, 2, 3, ...</c> with no bound. A bound here would be a <i>budget</i> - it
    /// buys a legible refusal in place of an astronomically long but finite run - and never a
    /// correctness device, since the search is already proven to terminate. Imposing one is a
    /// caller's decision about what it can afford to wait for, not this type's, so it is not taken
    /// here.
    /// </para>
    /// <para>
    /// <b>The terminating candidate is the least-height rational the enclosure contains, without
    /// qualification</b> - which is where this type differs from <see cref="DenominatorSweep"/>,
    /// whose terminal is the least-<i>denominator</i> one and can carry a larger height than some
    /// rational it ordered past. Above one the argument is in two cases. If the enclosure reaches
    /// down to one then it contains one, since its value exceeds one, so height 1 is enclosed and
    /// the first candidate finds it. If it does not, every rational it contains exceeds one and so
    /// has <c>|p| &gt; q</c>, which is exactly the family enumerated here. Below one the argument
    /// is <see cref="DenominatorSweep"/>'s together with the observation that an enclosure
    /// containing any of <c>-1</c>, <c>0</c> or <c>1</c> yields one of them at denominator 1, all
    /// three of which have height 1.
    /// </para>
    /// </remarks>
    public IEnumerable<RationalCandidate> Search(Approximation enclosure) =>
        SearchesNumerators(enclosure)
            ? SearchNumerators(enclosure)
            : Denominators.Search(enclosure);

    /// <summary>
    /// Finds the denominator whose rational of the given numerator is closest to the target.
    /// </summary>
    /// <param name="numerator">The numerator, carrying the target's sign. Never zero.</param>
    /// <param name="target">The target to approach. Its magnitude exceeds one.</param>
    /// <returns>The positive denominator minimising the distance to <paramref name="target"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Internal rather than private so that the tie rule below is testable at all.</b> The
    /// choice this method makes is invisible from the search's output: measured over 34111 tie
    /// configurations, flipping the rule changes no yielded sequence, because a numerator whose
    /// ideal denominator falls exactly halfway between two integers is fitting the target so badly
    /// that neither of its candidates is ever an improvement. A rule nothing can observe is a rule
    /// nothing can defend, so the decision is lifted out of the loop and handed to the tests as a
    /// pure function instead - the same seam this repository reaches for wherever a decision would
    /// otherwise be reachable only through machinery that hides it.
    /// </para>
    /// <para>
    /// <b>This is a bracket and an exact comparison, not a rounding, and the difference is the
    /// whole correctness of the axis.</b> At a fixed denominator the closest numerator is a
    /// nearest-rounding of <c>x*b</c>, because the numerator enters <c>p/b - x</c> linearly. At a
    /// fixed numerator it is not: <c>a/b</c> is a hyperbola in <c>b</c>, so the integer nearest
    /// <c>a/x</c> is not always the integer minimising <c>|a/b - x|</c>. Against <c>x = 29/10</c>
    /// and <c>a = 10</c> the nearest denominator is 3, at distance <c>13/30</c>, while 4 is closer
    /// at <c>2/5</c>. Rounding <c>a/x</c> and calling the result best would forfeit exhaustiveness
    /// exactly as a directed rounding would on the other axis, and just as quietly.
    /// </para>
    /// <para>
    /// So there is no rounding decision here to get wrong. <c>|a/b - x|</c> falls then rises about
    /// <c>a/|x|</c>, so the minimum over the integers is at one of the two brackets, and both are
    /// evaluated exactly. The floor's direction is not load-bearing - it only names which pair is
    /// examined - which is why this site does not join the two in
    /// <c>INSTRUCTIONS.md</c>'s rounding table.
    /// </para>
    /// <para>
    /// <b>Ties go to the smaller denominator</b>, which is the candidate further from zero. Either
    /// is sound, since they are equidistant, but an oracle must be deterministic. That is the same
    /// rule <see cref="DenominatorSweep.NumeratorRounding"/> encodes on the axis it varies -
    /// <see cref="MidpointRounding.AwayFromZero"/> picks the numerator of larger magnitude, so the
    /// candidate further from zero - and stating it as "further from zero" rather than "smaller
    /// denominator" is what makes it symmetric under negation, so a negative target's search is
    /// exactly the mirror of its positive twin's. An instance, since ties are rare enough to
    /// look accidental: against <c>15/4</c> the numerator 5 gives <c>5/1</c> and <c>5/2</c> at the
    /// same distance <c>5/4</c>, and both are reduced.
    /// </para>
    /// </remarks>
    internal static BigInteger BestDenominator(BigInteger numerator, BigRational target)
    {
        BigInteger lower = BigRational.Round(
            BigRational.FromInteger(BigInteger.Abs(numerator)) / BigRational.Abs(target),
            MidpointRounding.ToNegativeInfinity);

        if (lower < BigInteger.One)
        {
            lower = BigInteger.One;
        }

        BigInteger upper = lower + BigInteger.One;

        BigRational atLower = BigRational.Abs(new BigRational(numerator, lower) - target);
        BigRational atUpper = BigRational.Abs(new BigRational(numerator, upper) - target);

        return atUpper < atLower ? upper : lower;
    }

    /// <summary>The numerator axis, reached only when the target's magnitude exceeds one.</summary>
    private static IEnumerable<RationalCandidate> SearchNumerators(Approximation enclosure)
    {
        BigRational target = enclosure.Value;
        int sign = target.Sign;

        bool haveBest = false;
        BigRational bestDistance = BigRational.Zero;

        for (BigInteger height = BigInteger.One; ; height++)
        {
            BigInteger numerator = sign > 0 ? height : -height;
            BigInteger denominator = BestDenominator(numerator, target);

            // Skip a pair that is not already in lowest terms, so that 12/2 never revisits 6/1.
            // This is an early-out and NOT a correctness device: the reduced form a'/b' has a
            // smaller numerator, and index a' already took the *best* denominator for a', which is
            // therefore at least as close - so a non-reduced pair can never be a strict
            // improvement and the filter below would drop it anyway. Measured over 408 enclosures,
            // running with and without this skip produces identical output.
            //
            // That redundancy holds only because the denominator is chosen by comparing distances;
            // under a rounded a/x the "best at a'" step of the argument fails, and the two would
            // stop agreeing. Removing the rounding and the skip together would look like one
            // simplification and be two defects.
            if (BigInteger.GreatestCommonDivisor(height, denominator) != BigInteger.One)
            {
                continue;
            }

            BigRational value = new(numerator, denominator);
            BigRational distance = BigRational.Abs(value - target);

            if (haveBest && distance >= bestDistance)
            {
                continue;
            }

            haveBest = true;
            bestDistance = distance;

            RationalCandidate candidate = RationalCandidate.Against(value, enclosure);
            yield return candidate;

            if (candidate.IsEnclosed)
            {
                yield break;
            }
        }
    }
}
