using System.Numerics;
using System.Runtime.CompilerServices;

namespace HalHeinrich.Numerics;

/// <summary>
/// The rationals a set of enclosures leaves standing: every <c>p/q</c> in lowest terms whose
/// denominator is at or below a given bound and which every one of the enclosures contains. The
/// base of a closed set of walks that find them; <see cref="DenominatorWalk"/> is the reference.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refutation is a proof rather than a trend.</b> A candidate lying outside an enclosure of the
/// unknown is not the unknown - permanently, whatever later enclosures do - so a candidate this
/// search drops can never come back. The result therefore only ever shrinks as enclosures are
/// added, which is what makes it evidence rather than a reading. It replaces looking in a
/// <see cref="TrendMatrix"/> for a row that persists while improving, a proxy defeated three
/// separate ways in the exploration behind halheinrich/Math#64: by a near miss four orders of
/// magnitude closer than the answer, by a plateau that stopped being flat when the height cap
/// moved, and by a rival that was both unchanged and falling.
/// </para>
/// <para>
/// <b>Deliberately not an <see cref="IRationalApproximator"/>.</b> That interface is a search
/// against one enclosure ending at the first candidate the enclosure contains, and it obliges an
/// implementation to be lazy, strictly improving, of increasing height, and terminating on
/// enclosure. This contract keeps only the first of those: many enclosures rather than one, no
/// ordering by distance or by height, and everything still standing rather than the first hit.
/// Claiming the kinship would make the compiler accept callers that cannot be correct.
/// </para>
/// <para>
/// <b>An abstract base rather than an interface</b>, which is where this type departs from
/// <see cref="IRationalApproximator"/>. Two reasons, and neither applies there. The eager
/// validation of <see cref="Survivors"/> - null, empty and negative-bound refusals at the call,
/// the enclosures read once and copied - is contract, not courtesy: an implementation skipping it
/// would report an argument fault at some later <c>foreach</c>, or complete refutation from no
/// evidence. On an interface every implementation would restate it and could omit it; here it is
/// written once, in a member no implementation can override. And the constructor is
/// <c>private protected</c>, so the implementations are exactly the ones this assembly declares.
/// That closed set is what makes "every <see cref="SurvivorSearch"/> is cross-checked against the
/// reference" a true sentence rather than a hope about code not yet written.
/// <see cref="IRationalApproximator"/> has no argument to validate, since every enclosure is a
/// valid one; and its openness is used, since the test harness wraps an implementation in a
/// budgeted decorator, which a closed type would forbid.
/// </para>
/// <para>
/// <b>The element type is <see cref="BigRational"/> and not <see cref="RationalCandidate"/>.</b> A
/// candidate binds a value to the single enclosure it was judged against, so its
/// <see cref="RationalCandidate.IsEnclosed"/>, <see cref="RationalCandidate.MinDistance"/> and
/// <see cref="RationalCandidate.MaxDistance"/> are all relative to that one enclosure. Returned
/// from a search over many it would carry a property that reads as "survives" and does not mean
/// it. A caller wanting distances holds the enclosures already. Canonical form is therefore not a
/// call-site convention: <see cref="BigRational"/> is always in lowest terms with a positive
/// denominator, so no survivor can be a mis-spelling of another.
/// </para>
/// <para>
/// <b>It also carries the two rules that say when a known rational target survives alone</b> -
/// <see cref="IsReachable"/>, and <see cref="IsIsolated"/> with <see cref="ExclusiveIsolationBound"/>
/// the number it decides against. <c>SPEC-rational-ratio.md</c> § 2 is the only statement of both
/// and of the model behind them; they are implemented here once so that no caller recomputes
/// either, after a restated copy of the isolation rule was wrong three times in prose. The two fail
/// in opposite directions - a target the bound cannot reach is refuted falsely, one the enclosure
/// cannot isolate stands among rivals - which is why no member answers both at once. A single
/// yes-or-no would fold them back into the one rule they were once misread as.
/// </para>
/// </remarks>
public abstract class SurvivorSearch
{
    private const string NoEnclosuresMessage =
        "A survivor search needs at least one enclosure. With none, nothing is refuted and every " +
        "rational of the given denominator bound survives - infinitely many of them, since the " +
        "numerator is unbounded. Returning an empty result instead would report total refutation " +
        "from no evidence at all.";

    private const string NegativeBoundMessage =
        "A denominator bound is a largest denominator to consider and cannot be negative. Zero is " +
        "permitted and admits nothing, since a denominator is positive.";

    private const string UnreachableIsolationMessage =
        "Isolation presupposes reachability, and this denominator bound is below the target's own " +
        "denominator. The search proposes a rational only at its own denominator, so here the " +
        "target is not a candidate and the survivor set is empty rather than crowded - there is no " +
        "true yes and no true no to whether the target is isolated. Answering no would report " +
        "rivals standing beside the target, which is the opposite failure to the one that would " +
        "actually occur. IsReachable decides this before a run.";

    private const string NegativeHalfWidthMessage =
        "A half-width is the radius of an enclosure and cannot be negative. Zero is permitted: an " +
        "exact enclosure holds only its own value.";

    /// <summary>
    /// Admits implementations from this assembly only, so the set of walks is the closed one the
    /// type's remarks rely on.
    /// </summary>
    private protected SurvivorSearch()
    {
    }

    /// <summary>
    /// Finds the rationals of bounded denominator that none of the given enclosures excludes.
    /// </summary>
    /// <param name="enclosures">
    /// Enclosures of one unknown, every one of which a survivor must satisfy. Must hold at least
    /// one. Read once, so a caller may pass a lazy sequence.
    /// </param>
    /// <param name="denominatorBound">
    /// The largest denominator to consider. Zero admits nothing, since a denominator is positive.
    /// </param>
    /// <returns>
    /// A lazy sequence of every <c>p/q</c> in lowest terms with <c>q</c> at or below
    /// <paramref name="denominatorBound"/> that lies inside every enclosure, each yielded exactly
    /// once, in the order the implementation states.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> holds no enclosure.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominatorBound"/> is negative.</exception>
    /// <remarks>
    /// <para>
    /// <b>This base promises no order.</b> Each implementation states its own, and a caller that
    /// depends on one holds that implementation. It is the position <c>SPEC-rational-ratio.md</c>
    /// § 3 takes on <see cref="IRationalApproximator"/>, whose terminal belongs to the
    /// implementation's order and not to the interface - and for the same reason: the set is the
    /// claim, and an order is a property of the walk that produced it.
    /// </para>
    /// <para>
    /// <b>Lazy</b>, so the survivors are yielded as they are found and a caller who only wants to
    /// know whether anything survives can stop at the first. A caller who wants them as a
    /// collection materialises the sequence itself.
    /// </para>
    /// <para>
    /// <b>An empty <paramref name="enclosures"/> throws rather than returning nothing.</b> Nothing
    /// refutes, so every rational within the bound survives and there are infinitely many, the
    /// numerator being unbounded; an empty result would instead report complete refutation from no
    /// evidence. <c>AGENTS.md</c> § Exactness discipline - report the bound, not the verdict -
    /// forbids exactly that direction of error, and it is the direction every choice here leans
    /// against.
    /// </para>
    /// </remarks>
    public IEnumerable<BigRational> Survivors(
        IEnumerable<Approximation> enclosures,
        BigInteger denominatorBound)
    {
        ArgumentNullException.ThrowIfNull(enclosures);
        ThrowIfInvalidBound(denominatorBound);

        // Copied rather than held lazily: the enclosures are walked once per candidate, so a
        // caller's own sequence must not be re-enumerated - nor be free to change mid-search.
        Approximation[] all = [.. enclosures];

        if (all.Length == 0)
        {
            throw new ArgumentException(NoEnclosuresMessage, nameof(enclosures));
        }

        // Validation is eager and enumeration is deferred, which an iterator method alone cannot
        // do: its body does not run until the first MoveNext, so a caller would see an argument
        // fault at the foreach rather than at the call that caused it.
        return Enumerate(all, denominatorBound);
    }

    /// <summary>
    /// Determines whether a survivor search under this denominator bound proposes the target at
    /// all - the reachability rule of <c>SPEC-rational-ratio.md</c> § 2.
    /// </summary>
    /// <param name="target">A rational whose survival is in question. Any sign, including zero.</param>
    /// <param name="denominatorBound">The bound a search would run under, as <see cref="Survivors"/> takes it.</param>
    /// <returns>
    /// <see langword="true"/> exactly when <paramref name="denominatorBound"/> is at or above the
    /// denominator of <paramref name="target"/> in lowest terms.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominatorBound"/> is negative.</exception>
    /// <remarks>
    /// <para>
    /// <b>Below it the target cannot be a survivor, whatever the enclosures.</b> A survivor set that
    /// lacks a true answer reads as that answer refuted, which is the one direction of error this
    /// bench forbids. So this is a precondition for a control, checkable before any run; it says
    /// nothing about rivals, which is <see cref="IsIsolated"/>'s question.
    /// </para>
    /// <para>
    /// <b>A bound of zero is <see langword="false"/> rather than refused</b>, because it is a bound
    /// <see cref="Survivors"/> accepts: it examines nothing, and nothing is exactly what it would
    /// return. Which bounds are valid is decided once for this type, and every member taking one
    /// defers to that decision.
    /// </para>
    /// <para>
    /// The denominator read is the reduced one, since <see cref="BigRational"/> is always in lowest
    /// terms - a target spelled <c>12/2</c> is <c>6</c> and is reachable from a bound of 1.
    /// </para>
    /// </remarks>
    public static bool IsReachable(BigRational target, BigInteger denominatorBound)
    {
        ThrowIfInvalidBound(denominatorBound);

        return denominatorBound >= target.Denominator;
    }

    /// <summary>
    /// The exclusive upper bound on the half-width of an enclosure that isolates the target under
    /// this denominator bound: <c>1/(2*Q*b)</c> for a bound <c>Q</c> and a target of denominator
    /// <c>b</c> - the isolation rule of <c>SPEC-rational-ratio.md</c> § 2, as a number.
    /// </summary>
    /// <param name="target">A rational the bound reaches.</param>
    /// <param name="denominatorBound">The bound a search would run under, as <see cref="Survivors"/> takes it.</param>
    /// <returns>
    /// The value every isolating half-width lies strictly below. At the value itself, an enclosure
    /// with the target at one end can reach a rival at some bounds - and at an integer target, at
    /// every bound.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="denominatorBound"/> is negative, or is below the denominator of
    /// <paramref name="target"/> so that <see cref="IsReachable"/> is <see langword="false"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>For sizing and for reporting; decide with <see cref="IsIsolated"/>.</b> The strictness is
    /// the detail a caller comparing against this value gets wrong, so the comparison is written
    /// once, there.
    /// </para>
    /// <para>
    /// <b>Exclusive, and named so, because this library's refinement contracts are inclusive.</b>
    /// <see cref="IRealConstant.StepFor"/> and <see cref="IRealConstant.ApproximateTo"/> stop at the
    /// first bound <i>at or below</i> a target, so refining <i>to</i> this value may return an
    /// enclosure of exactly this half-width - and one of those with the target at an end can reach
    /// a rival. <see cref="Approximation.Coarsen"/> compounds it: it rounds a half-width up to a
    /// power of two, and at an integer target under a power-of-two <c>Q</c> this value is itself a
    /// power of two, so an enclosure refined strictly inside it can be coarsened onto it. That one
    /// still leaves the target alone, since coarsening keeps a centre the narrower radius placed -
    /// but the half-width now held no longer <i>proves</i> it, and a claim this bench makes is only
    /// as good as its proof.
    /// </para>
    /// <para>
    /// <b>No name or value can repair that, because a strict bound has no inclusive equivalent over
    /// the rationals.</b> There is no largest rational below this value; the least upper bound of
    /// those that isolate is this value, which does not. So nothing can be handed to an
    /// at-or-below refiner as "exactly isolating". A caller refining towards isolation refines,
    /// asks <see cref="IsIsolated"/> of the half-width it actually holds - after any coarsening -
    /// and refines further while the answer is no. That loop ends for any refinement whose
    /// half-width tends to zero: the least power of two at or above a half-width is under twice
    /// it, so once a half-width is at or below half this value, its coarsening is strictly below.
    /// </para>
    /// <para>
    /// <b>Sufficient and uniformly tight, not the exact threshold at every bound.</b> Below it every
    /// enclosure containing the target excludes every rival, wherever its centre sits. At an
    /// integer target it is also exact; above denominator 1 it is attained only for some bounds,
    /// and § 2 says which. A half-width at or above it is therefore not, by itself, evidence that
    /// a rival survives.
    /// </para>
    /// <para>
    /// <b>Refused, not computed, when the bound cannot reach the target.</b> Isolation presupposes
    /// reachability: below the target's denominator the survivor set is empty rather than crowded,
    /// and a half-width that "isolates" nothing would be a number with no meaning.
    /// </para>
    /// </remarks>
    public static BigRational ExclusiveIsolationBound(BigRational target, BigInteger denominatorBound)
    {
        // IsReachable validates the bound as well, so a negative one is refused there with the
        // message Survivors gives - the validity rule is ThrowIfInvalidBound's, once.
        if (!IsReachable(target, denominatorBound))
        {
            throw new ArgumentOutOfRangeException(nameof(denominatorBound), UnreachableIsolationMessage);
        }

        return new BigRational(BigInteger.One, 2 * denominatorBound * target.Denominator);
    }

    /// <summary>
    /// Determines whether an enclosure of this half-width, containing the target, leaves the target
    /// as its only survivor under this denominator bound - the isolation rule of
    /// <c>SPEC-rational-ratio.md</c> § 2, decided.
    /// </summary>
    /// <param name="target">A rational the bound reaches.</param>
    /// <param name="denominatorBound">The bound a search would run under, as <see cref="Survivors"/> takes it.</param>
    /// <param name="halfWidth">
    /// The half-width of the enclosure in question - for one already held, its
    /// <see cref="Approximation.MaxError"/> as held, after any coarsening.
    /// </param>
    /// <returns>
    /// <see langword="true"/> exactly when <paramref name="halfWidth"/> is strictly below
    /// <see cref="ExclusiveIsolationBound"/>. Where it is, every enclosure of that half-width
    /// containing <paramref name="target"/> has <paramref name="target"/> as its only survivor at
    /// the bound, wherever its centre sits - and since survivors only shrink as enclosures are
    /// added, so does any set of enclosures including one.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="denominatorBound"/> is negative or below the denominator of
    /// <paramref name="target"/>, or <paramref name="halfWidth"/> is negative.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>This is where the strictness lives</b>, and it is the reason the member exists beside
    /// <see cref="ExclusiveIsolationBound"/>: at the bound itself a rival is reachable, so a
    /// caller's own <c>&lt;=</c> in place of <c>&lt;</c> admits one. It is defined in terms of that
    /// number, so the two cannot disagree.
    /// </para>
    /// <para>
    /// <b>A half-width rather than an enclosure</b>, because both rules are wanted before a run,
    /// when no enclosure exists yet. Whether the enclosure actually contains the target is the
    /// provider's contract and not this rule's.
    /// </para>
    /// <para>
    /// <b>A no is a claim about the bound and not about the search.</b> The bound is sufficient and
    /// not exact above denominator 1, so a half-width this refuses may still leave the target alone
    /// at some bounds. At an integer target the two coincide.
    /// </para>
    /// <para>
    /// <b>Refused rather than answered when the bound cannot reach the target</b>, for the reason
    /// <see cref="ExclusiveIsolationBound"/> gives: a no would report the crowded failure for what
    /// is really the empty one.
    /// </para>
    /// </remarks>
    public static bool IsIsolated(BigRational target, BigInteger denominatorBound, BigRational halfWidth)
    {
        BigRational exclusiveBound = ExclusiveIsolationBound(target, denominatorBound);

        if (halfWidth.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(halfWidth), NegativeHalfWidthMessage);
        }

        return halfWidth < exclusiveBound;
    }

    /// <summary>
    /// Refuses a denominator bound no member of this type accepts. The one statement of what a
    /// valid bound is, so the members taking one cannot come to disagree about it.
    /// </summary>
    /// <param name="denominatorBound">The bound to check.</param>
    /// <param name="parameterName">The caller's name for it, captured rather than typed.</param>
    private static void ThrowIfInvalidBound(
        BigInteger denominatorBound,
        [CallerArgumentExpression(nameof(denominatorBound))] string? parameterName = null)
    {
        if (denominatorBound.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, NegativeBoundMessage);
        }
    }

    /// <summary>
    /// The walk itself, over the arguments <see cref="Survivors"/> has already validated and copied.
    /// </summary>
    /// <param name="enclosures">The caller's enclosures, copied and owned by this call; never empty.</param>
    /// <param name="denominatorBound">The largest denominator to consider; never negative.</param>
    /// <returns>
    /// The survivors, lazily and each exactly once, in the order the implementation states.
    /// </returns>
    private protected abstract IEnumerable<BigRational> Enumerate(
        Approximation[] enclosures,
        BigInteger denominatorBound);
}
