using System.Numerics;

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

        if (denominatorBound.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominatorBound), NegativeBoundMessage);
        }

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
