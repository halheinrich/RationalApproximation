using System.Numerics;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Test doubles for <see cref="IRealConstant"/>, each with an exactly known error function.
/// </summary>
/// <remarks>
/// <para>
/// These are <b>not</b> the stub-instead-of-the-real-thing case the contract warns about. The
/// behaviour under test is <see cref="IRealConstant"/>'s <i>defaulted</i> members,
/// <see cref="IRealConstant.StepFor"/> and <see cref="IRealConstant.ApproximateTo"/>, which are
/// production code that happens to live on an interface. A double is their input, not a stand-in
/// for them.
/// </para>
/// <para>
/// Testing them against a real provider would be worse, not better: the expected step would have
/// to be recomputed from that provider's own convergence formula, so an error in the formula
/// would cancel against the same error in the expectation and the test would pass. A double whose
/// crossing points are known by inspection cannot do that. Please do not "fix" this by
/// substituting a real constant when step 5 lands one.
/// </para>
/// </remarks>
internal static class TestConstants
{
    /// <summary>One half, as an exact rational.</summary>
    public static BigRational Half { get; } = new(1, 2);

    /// <summary>Two raised to a possibly negative power, exactly.</summary>
    public static BigRational PowerOfTwo(int exponent) =>
        exponent >= 0
            ? BigRational.FromInteger(BigInteger.One << exponent)
            : new BigRational(BigInteger.One, BigInteger.One << -exponent);
}

/// <summary>
/// A constant whose error bound halves at every step: <c>ErrorBoundAt(n)</c> is exactly
/// <c>1/2^n</c>, so the crossing points are readable by inspection. It converges to one.
/// </summary>
internal sealed class HalvingConstant : IRealConstant
{
    /// <summary>The value this constant converges to. Every refinement encloses it.</summary>
    public static BigRational Truth { get; } = BigRational.One;

    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        return TestConstants.PowerOfTwo(-step);
    }

    public IEnumerable<Approximation> Refinements()
    {
        // Incremental: each refinement is the previous one plus the next halving, never a
        // recomputation from scratch.
        BigRational error = BigRational.One;
        BigRational value = BigRational.Zero;

        while (true)
        {
            yield return Approximation.Create(value, error);

            error *= TestConstants.Half;
            value += error;
        }
    }
}

/// <summary>
/// A constant whose error bound is flat over runs of three steps: <c>ErrorBoundAt(n)</c> is
/// <c>1/2^(n/3)</c> with integer division. Non-increasing but not strictly decreasing, which is
/// what the contract permits and what <see cref="IRealConstant.StepFor"/> has to handle.
/// </summary>
/// <remarks>
/// The refinements themselves still improve strictly, since the bound is only an upper bound on
/// a step's error. That mismatch is realistic - a series with a crude bound behaves this way -
/// and it is what distinguishes <see cref="IRealConstant.StepFor"/> from
/// <see cref="IRealConstant.ApproximateTo"/>.
/// </remarks>
internal sealed class PlateauConstant : IRealConstant
{
    /// <summary>The value this constant converges to. Every refinement encloses it.</summary>
    public static BigRational Truth { get; } = BigRational.One;

    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        return TestConstants.PowerOfTwo(-(step / 3));
    }

    public IEnumerable<Approximation> Refinements()
    {
        BigRational bound = BigRational.One;
        int step = 0;

        while (true)
        {
            BigRational error = bound / BigRational.FromInteger(step + 1);
            yield return Approximation.Create(BigRational.One - error, error);

            step++;
            if (step % 3 == 0)
            {
                bound *= TestConstants.Half;
            }
        }
    }
}

/// <summary>
/// A halving constant that counts how many times its error bound was asked for, so a test can
/// show that <see cref="IRealConstant.StepFor"/> does not scan.
/// </summary>
/// <remarks>
/// The counter is a measuring instrument and does make this double impure, which is why it is a
/// separate type: <see cref="HalvingConstant"/> stays a faithful model of the contract.
/// </remarks>
internal sealed class CallCountingConstant : IRealConstant
{
    /// <summary>Gets the number of <see cref="ErrorBoundAt"/> calls made so far.</summary>
    public int ErrorBoundCalls { get; private set; }

    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        ErrorBoundCalls++;
        return TestConstants.PowerOfTwo(-step);
    }

    public IEnumerable<Approximation> Refinements() => new HalvingConstant().Refinements();
}

/// <summary>
/// A transparent decorator over any provider, counting how many refinement sequences were started
/// and how many refinements were pulled from them.
/// </summary>
/// <remarks>
/// <para>
/// The instrument for the two obligations that have no other witness: a consumer that is
/// <b>lazy</b> pulls nothing until it enumerates, and a consumer that is <b>incremental</b> starts
/// one sequence and advances it rather than restarting. Both are invisible in a return value, so
/// they are measured here rather than asserted about.
/// </para>
/// <para>
/// Separate from <see cref="CallCountingConstant"/> because the two measure different axes and are
/// used against different claims: that one is a provider whose <i>bound</i> calls are counted, to
/// show <see cref="IRealConstant.StepFor"/> does not scan; this one wraps an arbitrary provider
/// and counts its <i>refinements</i>, to show a consumer does not restart. Neither is a stand-in
/// for the behaviour under test.
/// </para>
/// <para>
/// The count of started sequences increments on the first pull rather than on the call to
/// <see cref="Refinements"/>, because an iterator's body does not run until it is enumerated -
/// which is exactly the laziness being measured.
/// </para>
/// </remarks>
/// <param name="inner">The provider to pass through and count.</param>
internal sealed class CountingConstant(IRealConstant inner) : IRealConstant
{
    /// <summary>Gets the number of refinement sequences that have been enumerated at all.</summary>
    public int SequencesStarted { get; private set; }

    /// <summary>Gets the total number of refinements pulled across every sequence.</summary>
    public int RefinementsPulled { get; private set; }

    public BigRational ErrorBoundAt(int step) => inner.ErrorBoundAt(step);

    public IEnumerable<Approximation> Refinements()
    {
        SequencesStarted++;

        foreach (Approximation refinement in inner.Refinements())
        {
            RefinementsPulled++;
            yield return refinement;
        }
    }
}

/// <summary>
/// A deliberately defective constant whose refinements run out. Used to show that
/// <see cref="IRealConstant.ApproximateTo"/> reports a broken implementation rather than
/// returning something wrong.
/// </summary>
internal sealed class FiniteConstant : IRealConstant
{
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        return TestConstants.PowerOfTwo(-step);
    }

    public IEnumerable<Approximation> Refinements()
    {
        yield return Approximation.Create(BigRational.Zero, BigRational.One);
        yield return Approximation.Create(TestConstants.Half, TestConstants.Half);
    }
}

/// <summary>
/// A deliberately defective constant whose error bound never improves. Used to show that
/// <see cref="IRealConstant.StepFor"/> gives up and reports the violated obligation instead of
/// looping forever.
/// </summary>
internal sealed class StuckConstant : IRealConstant
{
    public BigRational ErrorBoundAt(int step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);
        return BigRational.One;
    }

    public IEnumerable<Approximation> Refinements()
    {
        while (true)
        {
            yield return Approximation.Create(BigRational.Zero, BigRational.One);
        }
    }
}
