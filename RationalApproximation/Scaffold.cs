using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// Placeholder that exists only so this assembly is non-empty and the
/// <c>BigRationalLibrary</c> project reference is exercised end to end — at
/// compile time, at test time, and by the build-and-test workflow.
/// </summary>
/// <remarks>
/// <para>
/// This type is <b>not</b> part of the design. The real surface of this layer
/// — the approximation contracts of <c>SPEC-rational-ratio.md</c> § 3 — is
/// deliberately absent from the birth commit so the scaffolding is reviewable
/// on its own. Delete this type when the first of those contracts lands.
/// </para>
/// </remarks>
public static class Scaffold
{
    /// <summary>
    /// Gets the exact rational one half, as a reduced <see cref="BigRational"/>.
    /// </summary>
    /// <value>The rational 1/2.</value>
    public static BigRational OneHalf { get; } = new(BigInteger.One, new BigInteger(2));
}
