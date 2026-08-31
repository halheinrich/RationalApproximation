namespace HalHeinrich.Numerics;

/// <summary>
/// A search for the simplest rational an enclosure permits its unknown to be.
/// </summary>
/// <remarks>
/// <para>
/// Named for what it produces rather than for a conclusion. The target's irrationality is the
/// open question the bench exists to probe, and some targets genuinely are rational, so a name
/// asserting otherwise would beg the question in every file that used it.
/// </para>
/// </remarks>
public interface IRationalApproximator
{
    /// <summary>Searches for rationals the given enclosure permits, simplest first.</summary>
    /// <param name="enclosure">The enclosure of the unknown to search against.</param>
    /// <returns>
    /// A lazy sequence of candidates, each strictly closer to the enclosure's value than the last
    /// and each of greater height, ending with the first candidate the enclosure contains.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The obligations on an implementation are:
    /// </para>
    /// <list type="bullet">
    /// <item><description><b>Lazy</b> - a candidate is computed when it is pulled, so a caller may stop early.</description></item>
    /// <item><description><b>Strictly improving</b> - each candidate is strictly closer to <see cref="Approximation.Value"/> than every candidate before it.</description></item>
    /// <item><description><b>Increasing height</b> - each candidate's <see cref="RationalCandidate.Height"/> is strictly greater than its predecessor's.</description></item>
    /// <item><description><b>Terminating on enclosure</b> - the sequence ends with the first candidate whose <see cref="RationalCandidate.IsEnclosed"/> is true, and yields nothing after it.</description></item>
    /// </list>
    /// <para>
    /// "Strictly closer" is measured against <see cref="Approximation.Value"/> rather than against
    /// the enclosure as a whole. Distances to an enclosure are intervals, and intervals are only
    /// partially ordered - the same objection that keeps <see cref="Approximation"/> off
    /// <see cref="IComparable{T}"/>. The value is the exact quantity being approximated, and
    /// ordering by distance to it is total. In practice the choice is not observable: for any
    /// candidate outside the enclosure, distance to the value, <see cref="RationalCandidate.MinDistance"/>
    /// and <see cref="RationalCandidate.MaxDistance"/> all differ by the same constant
    /// <see cref="Approximation.MaxError"/> and so induce the same order, and every comparison a
    /// search makes is against a candidate outside the enclosure.
    /// </para>
    /// <para>
    /// A candidate inside the enclosure is always an improvement on everything before it, so the
    /// improvement filter and the stopping rule cannot conflict: being enclosed means being within
    /// <see cref="Approximation.MaxError"/> of the value, every earlier candidate was further away
    /// than that or the search would already have stopped, and so no enclosed candidate can be
    /// discarded as an unimprovement.
    /// </para>
    /// </remarks>
    public IEnumerable<RationalCandidate> Search(Approximation enclosure);
}
