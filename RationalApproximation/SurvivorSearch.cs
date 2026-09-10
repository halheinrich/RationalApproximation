using System.Numerics;
using System.Runtime.CompilerServices;

namespace HalHeinrich.Numerics;

/// <summary>
/// The rationals a set of enclosures leaves standing: every <c>p/q</c> in lowest terms whose
/// denominator is at or below a given bound and which every one of the enclosures contains.
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
/// Claiming the kinship would make the compiler accept callers that cannot be correct. Whether
/// this deserves an interface of its own is a question for a second implementation.
/// </para>
/// <para>
/// <b>The element type is <see cref="BigRational"/> and not <see cref="RationalCandidate"/>.</b> A
/// candidate binds a value to the single enclosure it was judged against, so its
/// <see cref="RationalCandidate.IsEnclosed"/>, <see cref="RationalCandidate.MinDistance"/> and
/// <see cref="RationalCandidate.MaxDistance"/> are all relative to that one enclosure. Returned
/// from a search over many it would carry a property that reads as "survives" and does not mean
/// it. A caller wanting distances holds the enclosures already.
/// </para>
/// <para>
/// <b>Canonical form is not a call-site convention here.</b> <see cref="BigRational"/> is always
/// in lowest terms with a positive denominator, so no survivor can be a mis-spelling of another;
/// what the enumeration has to avoid is proposing the same rational twice, once per denominator
/// that spells it. A scratchpad that skipped that step reported 1500 survivors which were 1500
/// spellings of <c>6</c> - the pairs <c>(6q, q)</c> for <c>q</c> from 1 to 1500.
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
public static class SurvivorSearch
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
    /// once.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="enclosures"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="enclosures"/> holds no enclosure.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="denominatorBound"/> is negative.</exception>
    /// <remarks>
    /// <para>
    /// <b>Ordering is promised: nondecreasing denominator, and increasing value within one
    /// denominator.</b> It is what the enumeration produces anyway, so it costs nothing, and it
    /// puts the simplest survivor first - which is the one a reader cares about, and the one a
    /// caller who only wants to know whether anything survives can stop at.
    /// </para>
    /// <para>
    /// <b>That is not height order</b>, and the difference shows up inside a single denominator
    /// rather than across them. An enclosure of <c>1 +/- 4</c> holds nine integers, so the first
    /// yielded is <c>-3/1</c> at height 3 while the <c>0/1</c> it also holds has height 1. Across
    /// denominators the two orders do agree for a narrow enclosure, since a rational near a fixed
    /// target has its height grow with its denominator, but that is a property of the enclosure
    /// and not a promise of this method. A caller wanting least height wants
    /// <see cref="HeightSweep"/>, which is the type that exists to make the distinction.
    /// </para>
    /// <para>
    /// <b>An empty <paramref name="enclosures"/> throws rather than returning nothing.</b> Nothing
    /// refutes, so every rational within the bound survives and there are infinitely many, the
    /// numerator being unbounded; an empty result would instead report complete refutation from no
    /// evidence. <c>AGENTS.md</c> § Exactness discipline - report the bound, not the verdict -
    /// forbids exactly that direction of error, and it is the direction every choice here leans
    /// against.
    /// </para>
    /// <para>
    /// <b>Enumeration runs against the narrowest enclosure; membership is decided against all of
    /// them.</b> One enclosure is enough to refute, so seeding the walk with the narrowest does
    /// nearly all the work for one enclosure's cost. The seed is a <i>cost</i> choice and not a
    /// correctness one - any of the enclosures would give the same answer, because a candidate the
    /// seed rejects is refuted by the seed and so is no survivor either way. Intersecting is still
    /// strictly stronger than filtering on any one of them, because enclosures need not nest: two
    /// of half-width <c>1/10</c> centred on <c>6</c> and on <c>61/10</c> admit seven candidates of
    /// denominator at or below 10 between them and share only two. This is why the seed is not
    /// documented as "the last" or "the best" enclosure: there is no such thing here.
    /// </para>
    /// <para>
    /// <b>The candidate space is never materialised.</b> Candidates are walked one at a time and
    /// dropped at the first enclosure that excludes them, so the reachable bound is limited by
    /// time and not by memory - the obvious implementation, which collects the candidates and then
    /// filters, exhausted memory when this arc's own sizing was first measured. The survivors are
    /// yielded as they are found and are usually a handful; a caller who wants them as a
    /// collection materialises the sequence itself.
    /// </para>
    /// <para>
    /// <b>Every arithmetic step deciding a candidate's fate is exact.</b> The numerators to try at
    /// each denominator come from directed rounding of exact rationals, and membership is
    /// <see cref="Approximation.Contains"/>, which compares <see cref="BigRational"/> values. A
    /// floating-point range bound that landed one short would drop a candidate silently, which is
    /// again an overstatement of refutation rather than an understatement.
    /// </para>
    /// </remarks>
    public static IEnumerable<BigRational> Survivors(
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

    /// <summary>The walk itself, over pre-validated arguments.</summary>
    private static IEnumerable<BigRational> Enumerate(
        Approximation[] enclosures,
        BigInteger denominatorBound)
    {
        Approximation seed = Narrowest(enclosures);

        for (BigInteger denominator = BigInteger.One; denominator <= denominatorBound; denominator++)
        {
            // The integers p with lo <= p/q <= hi are exactly those with lo*q <= p <= hi*q, so the
            // range is a ceiling and a floor of exact rationals - the definition of "which
            // integers are in this interval", not an approximation of it.
            //
            // This is NOT a third entry in INSTRUCTIONS.md's rounding table, because the direction
            // is not what is load-bearing. A wider range is merely wasteful: an extra candidate is
            // put to SurvivesAll, which tests the seed along with every other enclosure and drops
            // it. What must not happen is a range one short, and the way to get one short is a
            // float bound rather than a wrong direction - so the discipline here is exactness,
            // which AGENTS.md governs already.
            //
            // That safety is the seed being re-tested below rather than assumed, so this exact
            // range and that uniform test are redundant with each other. Dropping both would look
            // like one simplification and be two defects.
            BigInteger smallest = BigRational.Round(
                seed.Lower * denominator,
                MidpointRounding.ToPositiveInfinity);

            BigInteger largest = BigRational.Round(
                seed.Upper * denominator,
                MidpointRounding.ToNegativeInfinity);

            for (BigInteger numerator = smallest; numerator <= largest; numerator++)
            {
                // Skip a pair not already in lowest terms, so 12/2 never re-proposes 6/1. Unlike
                // HeightSweep's identical-looking skip this one is load-bearing: nothing
                // downstream filters, so without it a survivor is reported once per denominator
                // that spells it.
                if (BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator)
                    != BigInteger.One)
                {
                    continue;
                }

                BigRational candidate = new(numerator, denominator);
                if (SurvivesAll(candidate, enclosures))
                {
                    yield return candidate;
                }
            }
        }
    }

    /// <summary>Determines whether every enclosure permits the unknown to be this rational.</summary>
    /// <remarks>
    /// Stops at the first enclosure that excludes the candidate. Which one that is carries no
    /// weight - one refutation is a proof and a second adds nothing to it - so the index is not
    /// reported.
    /// </remarks>
    private static bool SurvivesAll(BigRational candidate, Approximation[] enclosures)
    {
        foreach (Approximation enclosure in enclosures)
        {
            if (!enclosure.Contains(candidate))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Picks the enclosure with the least <see cref="Approximation.MaxError"/>, which is the one
    /// admitting the fewest candidates per denominator.
    /// </summary>
    /// <remarks>
    /// Ties go to the first, and the choice is unobservable: the answer is the intersection over
    /// every enclosure whichever one seeds the walk.
    /// </remarks>
    private static Approximation Narrowest(Approximation[] enclosures)
    {
        Approximation narrowest = enclosures[0];

        foreach (Approximation enclosure in enclosures)
        {
            if (enclosure.MaxError < narrowest.MaxError)
            {
                narrowest = enclosure;
            }
        }

        return narrowest;
    }
}
