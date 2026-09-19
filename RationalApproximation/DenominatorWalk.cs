using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// The reference <see cref="SurvivorSearch"/>: walk every denominator from 1 to the bound, and at
/// each one put every numerator the narrowest enclosure admits to every enclosure.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is never optimised.</b> Its only product is being an oracle nobody has to argue
/// about, so it stays trivially auditable and slow - linear in the bound, one denominator at a
/// time. <c>AGENTS.md</c> § Exactness discipline: sitting behind a base type hedges against bad
/// choices, not against a defect in the thing every other choice is checked against. A faster walk
/// is another implementation beside this one, never a change here.
/// </para>
/// <para>
/// <b>Ordering is promised: nondecreasing denominator, and increasing value within one
/// denominator.</b> It is what the walk produces anyway, so it costs nothing, and it puts the
/// simplest survivor first - which is the one a reader cares about, and the one a caller who only
/// wants to know whether anything survives can stop at.
/// </para>
/// <para>
/// <b>That is not height order</b>, and the difference shows up inside a single denominator rather
/// than across them. An enclosure of <c>1 +/- 4</c> holds nine integers, so the first yielded is
/// <c>-3/1</c> at height 3 while the <c>0/1</c> it also holds has height 1. Across denominators the
/// two orders do agree for a narrow enclosure, since a rational near a fixed target has its height
/// grow with its denominator, but that is a property of the enclosure and not a promise of this
/// type. A caller wanting least height wants <see cref="HeightSweep"/>, which is the type that
/// exists to make the distinction.
/// </para>
/// <para>
/// <b>Enumeration runs against the narrowest enclosure; membership is decided against all of
/// them.</b> One enclosure is enough to refute, so seeding the walk with the narrowest does nearly
/// all the work for one enclosure's cost. The seed is a <i>cost</i> choice and not a correctness
/// one - any of the enclosures would give the same answer, because a candidate the seed rejects is
/// refuted by the seed and so is no survivor either way. Intersecting is still strictly stronger
/// than filtering on any one of them, because enclosures need not nest: two of half-width
/// <c>1/10</c> centred on <c>6</c> and on <c>61/10</c> admit seven candidates of denominator at or
/// below 10 between them and share only two. This is why the seed is not documented as "the last"
/// or "the best" enclosure: there is no such thing here.
/// </para>
/// <para>
/// <b>The candidate space is never materialised.</b> Candidates are walked one at a time and
/// dropped at the first enclosure that excludes them, so the reachable bound is limited by time and
/// not by memory - the obvious implementation, which collects the candidates and then filters,
/// exhausted memory when this arc's own sizing was first measured.
/// </para>
/// <para>
/// <b>The same rational must not be proposed twice</b>, once per denominator that spells it. A
/// scratchpad that skipped that step reported 1500 survivors which were 1500 spellings of
/// <c>6</c> - the pairs <c>(6q, q)</c> for <c>q</c> from 1 to 1500.
/// </para>
/// <para>
/// <b>Every arithmetic step deciding a candidate's fate is exact.</b> The numerators to try at each
/// denominator come from directed rounding of exact rationals, and membership is
/// <see cref="Approximation.Contains"/>, which compares <see cref="BigRational"/> values. A
/// floating-point range bound that landed one short would drop a candidate silently, which is an
/// overstatement of refutation rather than an understatement.
/// </para>
/// </remarks>
public sealed class DenominatorWalk : SurvivorSearch
{
    /// <summary>The walk itself, over pre-validated arguments.</summary>
    private protected override IEnumerable<BigRational> Enumerate(
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
