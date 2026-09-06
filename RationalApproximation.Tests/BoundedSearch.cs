using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// Runs a search to completion under two bounds.
/// </summary>
/// <remarks>
/// A defective search fails in two ways that inspecting its output cannot catch, because there is
/// no output to inspect: it can yield improvements forever, or spin without yielding at all.
/// Either way an unbounded test <i>hangs</i>, and a hanging test does not fail - it reports
/// nothing and burns the CI job's whole timeout, which is the same silence-reads-as-success mode
/// the build workflow's zero-test guard exists to prevent. The count cap catches the first mode
/// deterministically; the time budget catches the second, and latches so that one abandoned thread
/// is the worst case rather than one per assertion.
/// </remarks>
internal static class BoundedSearch
{
    private const int CandidateCap = 5000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    private static volatile bool overran;

    /// <summary>Enumerates a search fully, failing rather than hanging if it does not finish.</summary>
    public static List<RationalCandidate> RunToCompletion(IRationalApproximator approximator, Approximation enclosure)
    {
        Assert.False(overran, "An earlier search overran its budget; not starting another.");

        List<RationalCandidate>? candidates = null;
        Task task = Task.Run(() => candidates = [.. approximator.Search(enclosure).Take(CandidateCap)]);

        if (!task.Wait(Budget))
        {
            overran = true;
            Assert.Fail(Inv($"The search did not finish within {Budget.TotalSeconds} seconds for {enclosure.Value}."));
        }

        Assert.NotNull(candidates);
        Assert.True(
            candidates!.Count < CandidateCap,
            Inv($"The search yielded {CandidateCap} candidates without terminating for {enclosure.Value}."));

        return candidates!;
    }

    /// <summary>
    /// Wraps an approximator so that a search handed to a consumer cannot yield without end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the case <see cref="RunToCompletion"/> cannot reach: a consumer such as
    /// <see cref="ConstantRun"/> owns the enumeration, so a test cannot cap it from outside. This
    /// caps it from inside, on the same constant, so the two say one thing.
    /// </para>
    /// <para>
    /// <b>This is a budget, not a correctness device, and the distinction matters.</b>
    /// <see cref="DenominatorSweep"/> always terminates: the enclosure's <c>Value</c> is itself a
    /// <see cref="BigRational"/> <c>n/d</c>, so at denominator <c>d</c> the candidate is the centre
    /// and the enclosure contains it by definition. What the cap buys is therefore not termination
    /// but a <i>legible refusal</i> in place of an astronomically long but finite run. It catches
    /// an approximator that yields forever; it does not, and cannot, catch a sweep that is merely
    /// deep, because depth is invisible from outside the sequence. The depth budget is asserted
    /// separately, by naming a rational the enclosure already contains.
    /// </para>
    /// </remarks>
    public static IRationalApproximator Budgeted(IRationalApproximator approximator) =>
        new BudgetedApproximator(approximator);

    private sealed class BudgetedApproximator(IRationalApproximator inner) : IRationalApproximator
    {
        public IEnumerable<RationalCandidate> Search(Approximation enclosure)
        {
            int yielded = 0;

            foreach (RationalCandidate candidate in inner.Search(enclosure))
            {
                yielded++;
                if (yielded > CandidateCap)
                {
                    throw new InvalidOperationException(
                        Inv($"The search yielded more than {CandidateCap} candidates for {enclosure.Value} without terminating."));
                }

                yield return candidate;
            }
        }
    }
}
