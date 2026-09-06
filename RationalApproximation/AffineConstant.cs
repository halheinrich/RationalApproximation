namespace HalHeinrich.Numerics;

/// <summary>
/// The affine image of another real constant: <c>offset + scale * inner</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A combinator, not a constant.</b> It carries no series, no truncation and no convergence
/// argument of its own; everything it promises is the inner constant's promise, transformed. It is
/// parameterised by an arbitrary <see cref="IRealConstant"/> and knows nothing about which reals
/// are interesting, which is why it belongs on this layer rather than one above it. The layer's
/// rule that it holds no concrete constants bars <i>implementations</i> - a pi, a zeta, a square
/// root - and not abstractions over them.
/// </para>
/// <para>
/// <b>The bound is exactly <c>|scale|</c> times the inner's, and nothing rounds.</b> That is worth
/// stating rather than leaving to be noticed: bounds widen and never narrow throughout this
/// library, so a reader arriving here will look for the widening step and needs to be told there
/// is none. The reason is that the composition is built from
/// <see cref="Approximation.Multiply"/> and <see cref="Approximation.Add"/> against <i>exact</i>
/// operands. Multiplication's bound <c>|a|*beta + |b|*alpha + alpha*beta</c> collapses to
/// <c>|scale|*beta</c> when the scale's own radius <c>alpha</c> is zero, and addition's bound adds
/// the offset's radius, also zero. Both are exact <see cref="BigRational"/> operations, so the
/// result is an equality and not an inequality.
/// </para>
/// <para>
/// Composing through those two operations rather than computing the bound here is deliberate. The
/// propagation rule stays stated once, at the site the rest of the library is checked against, and
/// a negative scale then costs nothing: the <c>|scale|</c> comes out of the
/// <see cref="BigRational.Abs"/> already inside <see cref="Approximation.Multiply"/>, so a
/// reflection is handled by tested code instead of by a sign case written here.
/// </para>
/// <para>
/// <b>A zero scale is refused, and that is the contract speaking rather than defensiveness.</b>
/// <see cref="IRealConstant.Refinements"/> obliges the sequence to improve <i>strictly</i>. With a
/// zero scale every refinement would be the exact value <c>offset</c>, identical to its
/// predecessor and with a radius of zero, so the obligation could not be met by any inner constant
/// whatsoever. With <c>|scale| &gt; 0</c> the inner's strict improvement carries through unchanged,
/// scaled by a fixed positive factor, so no contract is weakened anywhere to admit this type.
/// </para>
/// <para>
/// <b>Nesting is lossless.</b> An <see cref="AffineConstant"/> wrapping another is again affine,
/// and because neither layer rounds, the composed bound is exactly the product of the two scales'
/// magnitudes times the innermost bound. Nothing accumulates.
/// </para>
/// <para>
/// Instances carry no mutable state, so one may be shared freely and every member is thread-safe.
/// Each call to <see cref="Refinements"/> returns a fresh, independent sequence, drawing a fresh
/// sequence from the inner constant.
/// </para>
/// </remarks>
public sealed class AffineConstant : IRealConstant
{
    private const string ScaleMustBeNonZeroMessage =
        "The scale must not be zero. A zero scale makes every refinement the exact value offset, " +
        "identical to its predecessor, which breaks the strictly-improving obligation of " +
        "IRealConstant.Refinements for every possible inner constant.";

    /// <summary>The offset as an exact enclosure, so the sum runs through the tested propagation.</summary>
    private readonly Approximation offsetPart;

    /// <summary>The scale as an exact enclosure, for the same reason.</summary>
    private readonly Approximation scalePart;

    /// <summary>The magnitude <c>|scale|</c>, which is the factor the inner's bound is scaled by.</summary>
    private readonly BigRational magnitude;

    /// <summary>Initialises the affine image <c>offset + scale * inner</c> of a real constant.</summary>
    /// <param name="offset">The constant added after scaling. May be zero, which is a pure scaling.</param>
    /// <param name="scale">
    /// The factor the inner constant is multiplied by. Must not be zero; may be negative, which
    /// reflects the inner constant about <paramref name="offset"/>.
    /// </param>
    /// <param name="inner">The constant being transformed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is zero.</exception>
    public AffineConstant(BigRational offset, BigRational scale, IRealConstant inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (scale.IsZero)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, ScaleMustBeNonZeroMessage);
        }

        Offset = offset;
        Scale = scale;
        Inner = inner;

        offsetPart = Approximation.Exact(offset);
        scalePart = Approximation.Exact(scale);
        magnitude = BigRational.Abs(scale);
    }

    /// <summary>Gets the constant added after scaling.</summary>
    public BigRational Offset { get; }

    /// <summary>Gets the factor the inner constant is multiplied by. Never zero.</summary>
    public BigRational Scale { get; }

    /// <summary>Gets the constant being transformed.</summary>
    /// <remarks>
    /// Exposed because an affine image is only meaningful alongside what it is an image of - a
    /// run that reports <c>offset</c> and <c>scale</c> without naming the inner constant has not
    /// said what it computed.
    /// </remarks>
    public IRealConstant Inner { get; }

    /// <summary>
    /// Gets the proven upper bound on the error of step <paramref name="step"/>, without computing
    /// that step.
    /// </summary>
    /// <param name="step">The zero-based step index, matching the position in <see cref="Refinements"/>.</param>
    /// <returns><c>|Scale|</c> times the inner constant's bound at the same step.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is negative.</exception>
    /// <remarks>
    /// Every obligation is inherited rather than re-argued. Purity, non-increase and cheapness
    /// survive multiplication by a fixed rational; the bound tends to zero because
    /// <c>|Scale|</c> is a fixed <i>positive</i> factor, which is the second place the refusal of
    /// a zero scale earns itself.
    /// </remarks>
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        return magnitude * Inner.ErrorBoundAt(step);
    }

    /// <summary>Gets the endless sequence of successively better approximations to this constant.</summary>
    /// <returns>A lazy, endless sequence of enclosures, one per inner refinement.</returns>
    /// <remarks>
    /// Laziness, endlessness and incrementality are the inner sequence's, passed straight through:
    /// exactly one inner refinement is pulled per element and nothing is buffered. Strict
    /// improvement is the inner's scaled by <c>|Scale| &gt; 0</c>, which preserves a strict
    /// inequality.
    /// </remarks>
    public IEnumerable<Approximation> Refinements()
    {
        foreach (Approximation refinement in Inner.Refinements())
        {
            yield return offsetPart + (scalePart * refinement);
        }
    }

    /// <summary>
    /// Gets the first step whose error bound is at or below the given target, by asking the inner
    /// constant for the step meeting the scaled-down target.
    /// </summary>
    /// <param name="targetError">The error to reach. Must be strictly positive.</param>
    /// <returns>The least step index <c>n</c> with <c>ErrorBoundAt(n) &lt;= targetError</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="targetError"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// The inner bound had not reached the scaled target by <see cref="int.MaxValue"/> steps, so
    /// the inner implementation is not tending to zero.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Re-declared rather than inherited, which <see cref="IRealConstant"/> invites provided the
    /// re-declaration delegates instead of reimplementing. This one delegates: there is no search
    /// here. The identity is exact, not approximate - since <c>|Scale| &gt; 0</c>,
    /// <c>|Scale| * B(n) &lt;= t</c> holds exactly when <c>B(n) &lt;= t / |Scale|</c>, and the
    /// division is exact <see cref="BigRational"/> arithmetic - so this returns the same step the
    /// interface's own search would, having done none of it. It also lets an inner constant with a
    /// better search than bisection supply it, which the default could not.
    /// </para>
    /// <para>
    /// A non-positive target is rejected by the inner's own check rather than by a copy of it
    /// here. The division cannot mask one: dividing by a strictly positive magnitude preserves the
    /// sign, so a target at or below zero arrives at the inner as a target at or below zero, and
    /// the exception names the same parameter with the same message.
    /// </para>
    /// <para>
    /// <b><see cref="IRealConstant.ApproximateTo"/> is deliberately not re-declared</b>, so it
    /// stays reachable only through an <see cref="IRealConstant"/>-typed reference. Delegating it
    /// would be exact by the same identity but would save nothing, since the refinements have to
    /// be pulled either way; and the members are not equivalent in kind. <see cref="StepFor"/>
    /// plans a run and costs no refinements, which is what a caller holding this type concretely
    /// wants; <c>ApproximateTo</c> restarts the inner sequence on every call, which is the one
    /// thing a run must not do. Exposing the plannable member and not that one is the asymmetry
    /// this type wants rather than an oversight.
    /// </para>
    /// </remarks>
    public int StepFor(BigRational targetError) => Inner.StepFor(targetError / magnitude);
}
