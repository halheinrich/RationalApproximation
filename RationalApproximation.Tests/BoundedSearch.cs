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
}
