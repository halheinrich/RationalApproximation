using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// The reference <see cref="IRationalApproximator"/>: sweep denominators upward, take the nearest
/// numerator at each, and keep only the ones that improve.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is never optimised.</b> Its only product is being an oracle nobody has to argue
/// about, so it stays trivially auditable and slow. A floating-point prefilter would in fact work
/// arithmetically, and is a standard technique, but it would put a numerically delicate
/// tie-detection threshold at exactly the decision this type exists to get right - with nothing
/// left to validate <i>it</i> against. Sitting behind an interface hedges against bad choices, not
/// against a defect in the thing every other choice is checked against. If a fast searcher is
/// wanted it is a third implementation, never a change here.
/// </para>
/// <para>
/// The sweep always terminates, including on an exact enclosure. An enclosure's value is a
/// <see cref="BigRational"/> and therefore itself rational, so at worst the sweep reaches that
/// value's own denominator and finds it exactly.
/// </para>
/// </remarks>
public sealed class DenominatorSweep : IRationalApproximator
{
    /// <summary>
    /// The rounding used to pick the best numerator at each denominator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a nearest rounding, and it has to be.</b> The claim that makes the sweep worth
    /// anything is exhaustiveness: if the closest rational of denominator <c>b</c> misses the
    /// enclosure then every rational of that denominator misses it. That holds only for the
    /// closest. A directed rounding - the ceiling used when coarsening an error bound, say - would
    /// forfeit the claim silently, leaving a search that still returns answers and no longer
    /// justifies them. This design rounds at two sites in opposite directions, and conflating
    /// them is a defect.
    /// </para>
    /// <para>
    /// Ties are equidistant, so either neighbour is sound, but an oracle must be deterministic.
    /// <see cref="MidpointRounding.AwayFromZero"/> is chosen over the other nearest mode,
    /// <see cref="MidpointRounding.ToEven"/>, because its tie rule is a single clause a reader can
    /// check by hand rather than one that depends on the parity of the answer, and because it is
    /// symmetric under negation, so a negative target's sweep is exactly the mirror of its
    /// positive twin's.
    /// </para>
    /// </remarks>
    public const MidpointRounding NumeratorRounding = MidpointRounding.AwayFromZero;

    /// <summary>Searches for rationals the given enclosure permits, simplest first.</summary>
    /// <param name="enclosure">The enclosure of the unknown to search against.</param>
    /// <returns>
    /// A lazy sequence of candidates of increasing height, each strictly closer to the enclosure's
    /// value than the last, ending with the first one the enclosure contains.
    /// </returns>
    /// <remarks>
    /// Denominators run 1, 2, 3, ... without a bound, because there is no bound to impose: the
    /// enclosure decides where the search stops, and imposing a second limit would let the search
    /// end quietly without an answer.
    /// </remarks>
    public IEnumerable<RationalCandidate> Search(Approximation enclosure)
    {
        BigRational target = enclosure.Value;

        bool haveBest = false;
        BigRational bestDistance = BigRational.Zero;

        for (BigInteger denominator = BigInteger.One; ; denominator++)
        {
            // The closest rational of this denominator. Everything downstream rests on this
            // being the closest rather than merely a close one; see NumeratorRounding.
            BigInteger numerator = BigRational.Round(target * denominator, NumeratorRounding);
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
