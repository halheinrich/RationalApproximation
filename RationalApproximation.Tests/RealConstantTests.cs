using static HalHeinrich.Numerics.Tests.Sampling;
using static HalHeinrich.Numerics.Tests.TestConstants;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The defaulted members of <see cref="IRealConstant"/>. The doubles they run against are
/// documented in <see cref="TestConstants"/>; they are input to the behaviour under test, not a
/// substitute for it.
/// </summary>
public class RealConstantTests
{
    // ---------- StepFor ----------

    [Fact]
    public void StepFor_FindsTheFirstStepMeetingTheTarget()
    {
        IRealConstant constant = new HalvingConstant();

        // 1/2^n <= 1/7 first at n = 3, since 1/4 > 1/7 >= 1/8.
        Assert.Equal(3, constant.StepFor(Ratio(1, 7)));

        // 1/2^n <= 1/1000 first at n = 10, since 2^9 = 512 < 1000 <= 1024.
        Assert.Equal(10, constant.StepFor(Ratio(1, 1000)));
    }

    [Fact]
    public void StepFor_ReturnsTheStepWhoseBoundEqualsTheTarget_NotTheNextOne()
    {
        IRealConstant constant = new HalvingConstant();

        // The boundary. ErrorBoundAt(3) is exactly 1/8, and "at or below" includes at.
        Assert.Equal(Ratio(1, 8), constant.ErrorBoundAt(3));
        Assert.Equal(3, constant.StepFor(Ratio(1, 8)));

        Assert.Equal(0, constant.StepFor(BigRational.One));
        Assert.Equal(1, constant.StepFor(Ratio(1, 2)));
    }

    [Fact]
    public void StepFor_ReturnsZero_WhenTheFirstStepAlreadyQualifies()
    {
        IRealConstant constant = new HalvingConstant();

        Assert.Equal(0, constant.StepFor(BigRational.One));
        Assert.Equal(0, constant.StepFor(Ratio(5, 1)));
    }

    [Fact]
    public void StepFor_OnAPlateau_ReturnsTheFirstStepOfTheRun()
    {
        IRealConstant constant = new PlateauConstant();

        // The bound is flat over each run of three: steps 3, 4 and 5 all bound at 1/2.
        Assert.Equal(Ratio(1, 2), constant.ErrorBoundAt(3));
        Assert.Equal(Ratio(1, 2), constant.ErrorBoundAt(4));
        Assert.Equal(Ratio(1, 2), constant.ErrorBoundAt(5));

        Assert.Equal(3, constant.StepFor(Ratio(1, 2)));
        Assert.Equal(6, constant.StepFor(Ratio(1, 4)));
        Assert.Equal(0, constant.StepFor(BigRational.One));
    }

    [Fact]
    public void StepFor_AgreesWithAnExhaustiveScanOverManyTargets()
    {
        // The bisection is the thing under test, so the expectation is a linear scan built from
        // the definition: the least n with ErrorBoundAt(n) <= target.
        IRealConstant halving = new HalvingConstant();
        IRealConstant plateau = new PlateauConstant();

        for (int denominator = 1; denominator <= 200; denominator++)
        {
            BigRational target = Ratio(1, denominator);

            Assert.Equal(ScanForStep(halving, target), halving.StepFor(target));
            Assert.Equal(ScanForStep(plateau, target), plateau.StepFor(target));
        }
    }

    [Fact]
    public void StepFor_DoesNotScan()
    {
        // ErrorBoundAt is cheap by contract, but "cheap" is not "free", and a linear scan would
        // make planning a deep run cost as much as the run. Doubling then bisecting should cost
        // on the order of twice the logarithm of the answer.
        var counting = new CallCountingConstant();
        IRealConstant constant = counting;

        int step = constant.StepFor(PowerOfTwo(-1000));

        Assert.Equal(1000, step);
        Assert.True(
            counting.ErrorBoundCalls <= 64,
            Inv($"StepFor took {counting.ErrorBoundCalls} bound evaluations to reach step {step}."));
    }

    [Fact]
    public void StepFor_RejectsANonPositiveTarget()
    {
        IRealConstant constant = new HalvingConstant();

        // A bound tends to zero without reaching it, so zero could never be met and the search
        // would not terminate. Refusing beats hanging.
        Assert.Throws<ArgumentOutOfRangeException>(() => constant.StepFor(BigRational.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => constant.StepFor(Ratio(-1, 4)));
    }

    [Fact]
    public void StepFor_ReportsAnImplementationThatNeverImproves()
    {
        IRealConstant constant = new StuckConstant();

        Assert.Throws<InvalidOperationException>(() => constant.StepFor(Ratio(1, 2)));
    }

    // ---------- ApproximateTo ----------

    [Fact]
    public void ApproximateTo_ReturnsTheFirstRefinementMeetingTheTarget()
    {
        IRealConstant constant = new HalvingConstant();

        // MaxError 1/2^n <= 1/100 first at n = 7, giving 127/128 +/- 1/128.
        Approximation result = constant.ApproximateTo(Ratio(1, 100));

        Assert.Equal(Ratio(127, 128), result.Value);
        Assert.Equal(Ratio(1, 128), result.MaxError);
    }

    [Fact]
    public void ApproximateTo_ReturnsTheRefinementWhoseErrorEqualsTheTarget_NotTheNextOne()
    {
        // The same inclusive boundary StepFor has, on the other default. Missing it costs a whole
        // extra refinement for every target that lands exactly on one - and every target derived
        // from a halving bound does.
        IRealConstant constant = new HalvingConstant();

        Approximation result = constant.ApproximateTo(Ratio(1, 8));

        Assert.Equal(Ratio(1, 8), result.MaxError);
        Assert.Equal(Ratio(7, 8), result.Value);
    }

    [Fact]
    public void ApproximateTo_ResultMeetsTheTargetAndStillEnclosesTheTruth()
    {
        IRealConstant constant = new HalvingConstant();

        foreach (int denominator in new[] { 2, 3, 10, 97, 1000, 65536 })
        {
            BigRational target = Ratio(1, denominator);
            Approximation result = constant.ApproximateTo(target);

            Assert.True(result.MaxError <= target, Inv($"MaxError {result.MaxError} missed target {target}."));
            Assert.True(result.Contains(HalvingConstant.Truth), Inv($"Refinement stopped enclosing the truth at target {target}."));
        }
    }

    [Fact]
    public void ApproximateTo_StopsOnTheRealisedErrorNotOnTheBound()
    {
        // PlateauConstant's bound is flat over runs of three while its refinements keep
        // improving, so the first refinement meeting a target arrives before the first step whose
        // bound does. Stopping on the bound would do strictly more work for the same answer.
        IRealConstant constant = new PlateauConstant();
        BigRational target = Ratio(1, 3);

        Approximation result = constant.ApproximateTo(target);
        int stepFromBound = constant.StepFor(target);

        int stepFromRefinements = 0;
        foreach (Approximation refinement in constant.Refinements())
        {
            if (refinement.MaxError <= target)
            {
                break;
            }

            stepFromRefinements++;
        }

        Assert.True(result.MaxError <= target);
        Assert.True(
            stepFromRefinements < stepFromBound,
            Inv($"Expected the refinement to arrive before the bound did: {stepFromRefinements} vs {stepFromBound}."));
    }

    [Fact]
    public void ApproximateTo_RejectsANonPositiveTarget()
    {
        IRealConstant constant = new HalvingConstant();

        Assert.Throws<ArgumentOutOfRangeException>(() => constant.ApproximateTo(BigRational.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => constant.ApproximateTo(Ratio(-1, 2)));
    }

    [Fact]
    public void ApproximateTo_ReportsAnImplementationWhoseRefinementsEnd()
    {
        IRealConstant constant = new FiniteConstant();

        Assert.Throws<InvalidOperationException>(() => constant.ApproximateTo(Ratio(1, 1000)));
    }

    // ---------- the obligations the doubles model ----------

    [Fact]
    public void Refinements_ImproveStrictlyAndKeepEnclosingTheTruth()
    {
        // Endless by contract, so a test takes a finite prefix. Refinements is a declared member
        // rather than a defaulted one, so the concrete type reaches it directly.
        var constant = new HalvingConstant();

        BigRational? previous = null;
        foreach (Approximation refinement in constant.Refinements().Take(40))
        {
            Assert.True(refinement.Contains(HalvingConstant.Truth));

            if (previous is BigRational earlier)
            {
                Assert.True(refinement.MaxError < earlier, Inv($"MaxError did not improve: {earlier} then {refinement.MaxError}."));
            }

            previous = refinement.MaxError;
        }
    }

    [Fact]
    public void Refinements_AreEndless()
    {
        Assert.Equal(500, ((IRealConstant)new HalvingConstant()).Refinements().Take(500).Count());
        Assert.Equal(500, ((IRealConstant)new PlateauConstant()).Refinements().Take(500).Count());
    }

    [Fact]
    public void ErrorBoundAt_BoundsTheRefinementAtTheSameStep()
    {
        // The bound is an upper bound on the step's error, so the realised MaxError may be
        // smaller but never larger.
        foreach (IRealConstant constant in new IRealConstant[] { new HalvingConstant(), new PlateauConstant() })
        {
            int step = 0;
            foreach (Approximation refinement in constant.Refinements().Take(30))
            {
                Assert.True(
                    refinement.MaxError <= constant.ErrorBoundAt(step),
                    Inv($"Step {step} realised {refinement.MaxError} against a bound of {constant.ErrorBoundAt(step)}."));
                step++;
            }
        }
    }

    [Fact]
    public void ErrorBoundAt_IsNonIncreasingAndTendsToZero()
    {
        foreach (IRealConstant constant in new IRealConstant[] { new HalvingConstant(), new PlateauConstant() })
        {
            BigRational previous = constant.ErrorBoundAt(0);
            for (int step = 1; step <= 60; step++)
            {
                BigRational current = constant.ErrorBoundAt(step);
                Assert.True(current <= previous, Inv($"Bound rose at step {step}: {previous} then {current}."));
                previous = current;
            }

            Assert.True(constant.ErrorBoundAt(200) < Ratio(1, 1000000));
        }
    }

    private static int ScanForStep(IRealConstant constant, BigRational target)
    {
        for (int step = 0; ; step++)
        {
            if (constant.ErrorBoundAt(step) <= target)
            {
                return step;
            }
        }
    }
}
