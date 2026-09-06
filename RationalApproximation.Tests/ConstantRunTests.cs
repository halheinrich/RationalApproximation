using System.Numerics;

using static HalHeinrich.Numerics.Tests.Sampling;
using static HalHeinrich.Numerics.Tests.TestConstants;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="ConstantRun"/>: its mechanics, the cost claim it makes, and the three controls it
/// carries because it inherits none.
/// </summary>
/// <remarks>
/// <para>
/// The runner is new code on a new path. The ratio pipeline's controls cover none of it, so the
/// same treatment is applied one layer down: a constant converging to a <b>known rational of
/// non-trivial height</b>, whose row must fall and whose run must end on it; a constant whose
/// answer is <b>out of reach</b>, whose every enclosure must exclude the previous iteration's
/// answer; and a <b>near-miss</b>, which reports one simple rational at every iteration and must
/// not be read as having found it.
/// </para>
/// <para>
/// <b>Every schedule here was chosen by measurement, not from a law.</b> Sweep depth is not a
/// function of the target error: at the same error of 3.8e-6 the near-miss control terminates at
/// denominator 1 and the out-of-reach control at 1597, and a target sitting just outside a
/// low-height rational can cost 1/(2*q*e) where a generic one costs e^(-1/2). The
/// out-of-reach schedule in particular was picked from three candidates because it is the one
/// whose answer advances at <i>every</i> step; a neighbouring schedule repeats a candidate and
/// would fail the exclusion criterion.
/// </para>
/// </remarks>
public class ConstantRunTests
{
    /// <summary>
    /// The positive control's limit: a known rational of non-trivial height. Height is the larger
    /// of the absolute numerator and the denominator, so this one is 355 - far above the
    /// denominator-1 answer a control converging to a small integer would hand the sweep for free.
    /// </summary>
    private static BigRational KnownAnswer { get; } = Ratio(355, 113);

    /// <summary>
    /// The out-of-reach control's limit: a ratio of consecutive Fibonacci numbers, whose continued
    /// fraction is all ones, so no low-height rational lies near it and the sweep must climb.
    /// </summary>
    private static BigRational OutOfReach { get; } = FibonacciRatio(60);

    /// <summary>The near-miss control's limit: a hair above a very simple rational.</summary>
    private static BigRational NearMiss { get; } = Ratio(6, 1) + BigRational.Pow(Ratio(1, 10), 30);

    // ---------- mechanics ----------

    [Fact]
    public void Execute_ProducesOneIterationAndOneColumnPerTarget()
    {
        BigRational[] schedule = [PowerOfTwo(-2), PowerOfTwo(-5), PowerOfTwo(-9)];

        ConstantRun run = Execute(new HalvingConstant(KnownAnswer), schedule);

        Assert.Equal(3, run.Iterations.Count);
        Assert.Equal(3, run.Matrix.Ratios.Count);

        for (int index = 0; index < schedule.Length; index++)
        {
            Assert.Equal(schedule[index], run.Iterations[index].TargetError);
            Assert.Equal(run.Iterations[index].Enclosure, run.Matrix.Ratios[index]);
        }
    }

    [Fact]
    public void Execute_DrivesEachIterationToItsTargetAndNoFurther()
    {
        // HalvingConstant realises exactly 1/2^n at step n, so a target that is itself a power of
        // two is met exactly. Meeting it with room to spare would mean the run went deeper than
        // the schedule asked.
        BigRational[] schedule = [PowerOfTwo(-3), PowerOfTwo(-7), PowerOfTwo(-11)];

        ConstantRun run = Execute(new HalvingConstant(KnownAnswer), schedule);

        int[] expectedSteps = [3, 7, 11];
        Assert.Equal(expectedSteps, run.Iterations.Select(iteration => iteration.Step));

        foreach (ConstantIteration iteration in run.Iterations)
        {
            Assert.Equal(iteration.TargetError, iteration.Enclosure.MaxError);
        }
    }

    [Fact]
    public void Execute_OnATargetAlreadyMet_AdvancesNothing()
    {
        // Needs a provider whose realised error can overshoot a target, which HalvingConstant
        // cannot: it realises exactly 1/2^n, so a power-of-two target is always met exactly and
        // never with room to spare. PlateauConstant realises 1, 1/2, 1/3, 1/8, ..., so step 3
        // overshoots a target of 1/7 and then meets 1/8 as well, at no further cost.
        BigRational[] schedule = [Ratio(1, 3), Ratio(1, 7), Ratio(1, 8)];

        ConstantRun run = Execute(new PlateauConstant(), schedule);

        int[] expectedSteps = [2, 3, 3];
        Assert.Equal(expectedSteps, run.Iterations.Select(iteration => iteration.Step));
        Assert.Equal(Ratio(1, 8), run.Iterations[^1].Enclosure.MaxError);
    }

    [Fact]
    public void Execute_DefaultsToTheDenominatorSweep()
    {
        BigRational[] schedule = [PowerOfTwo(-4), PowerOfTwo(-8)];

        ConstantRun byDefault = Execute(new HalvingConstant(KnownAnswer), schedule);
        ConstantRun explicitly = ConstantRun.Execute(
            new HalvingConstant(KnownAnswer), schedule, BoundedSearch.Budgeted(new DenominatorSweep()));

        Assert.Equal(
            byDefault.Iterations.Select(iteration => iteration.Simplest.Value),
            explicitly.Iterations.Select(iteration => iteration.Simplest.Value));
    }

    [Fact]
    public void Execute_OnAnEmptySchedule_IsHonestlyEmptyAndCostsNothing()
    {
        var counting = new CountingConstant(new HalvingConstant(KnownAnswer));

        ConstantRun run = Execute(counting, []);

        Assert.Empty(run.Iterations);
        Assert.Empty(run.Matrix.Ratios);
        Assert.Empty(run.Matrix.Rows);
        Assert.Equal(0, counting.SequencesStarted);
        Assert.Equal(0, counting.RefinementsPulled);
    }

    // ---------- the cost claim ----------

    [Fact]
    public void Execute_HoldsOneRefinementSequenceAcrossTheWholeRun()
    {
        // The claim is that the last iteration costs the depth it reaches, not that depth times
        // the number of iterations. Calling ApproximateTo once per target would restart the
        // sequence every time, so the count is what distinguishes the two implementations.
        var counting = new CountingConstant(new HalvingConstant(KnownAnswer));
        BigRational[] schedule = [PowerOfTwo(-2), PowerOfTwo(-6), PowerOfTwo(-11), PowerOfTwo(-20)];

        ConstantRun run = Execute(counting, schedule);

        Assert.Equal(20, run.Iterations[^1].Step);
        Assert.Equal(1, counting.SequencesStarted);

        // Twenty-one refinements for the whole run: steps 0 through 20 pulled exactly once each.
        Assert.Equal(21, counting.RefinementsPulled);

        // What a per-target ApproximateTo would have cost instead, for contrast: 3 + 7 + 12 + 21.
        Assert.True(counting.RefinementsPulled < 43);
    }

    // ---------- control one: a known rational answer ----------

    [Fact]
    public void PositiveControl_TheRunEndsOnTheKnownAnswerAndItsRowFallsToZero()
    {
        // 355/113 is 355 in height, so the sweep has to climb to it rather than meet it at
        // denominator 1 - which is the whole objection to a control converging to one. The
        // intermediate candidates are deliberately not asserted: which rationals an enclosure
        // admits on the way depends on where the provider places the truth inside it, so only the
        // destination is a property of the constant.
        BigRational[] schedule =
        [
            PowerOfTwo(-2), PowerOfTwo(-4), PowerOfTwo(-6), PowerOfTwo(-8),
            PowerOfTwo(-10), PowerOfTwo(-14), PowerOfTwo(-16), PowerOfTwo(-20),
        ];

        ConstantRun run = Execute(new HalvingConstant(KnownAnswer), schedule);

        Assert.Equal(KnownAnswer, run.Iterations[^1].Simplest.Value);
        Assert.Equal(new BigInteger(355), run.Iterations[^1].Simplest.Height);

        TrendRow row = RowFor(run, KnownAnswer);

        // The distance to the limit is exactly the iteration's own error, so the row falls with the
        // schedule and reaches zero only in the limit - never in the run.
        for (int column = 0; column < schedule.Length; column++)
        {
            Assert.Equal(schedule[column], row.Distances[column]);
            Assert.True(row.Distances[column].Sign > 0, "The row reached zero, which no finite run can.");
        }

        AssertStrictlyFalling(row);
    }

    // ---------- control two: an answer out of reach ----------

    [Fact]
    public void OutOfReachControl_EveryEnclosureExcludesThePreviousIterationsAnswer()
    {
        // The ratified criterion for a negative control, and the strong form of it. "Every row
        // must plateau" is satisfied trivially by a pipeline that has stopped narrowing, since
        // identical columns still leave one surviving row; exclusion is not.
        ConstantRun run = Execute(new HalvingConstant(OutOfReach), OutOfReachSchedule());

        for (int index = 1; index < run.Iterations.Count; index++)
        {
            BigRational previous = run.Iterations[index - 1].Simplest.Value;

            Assert.False(
                run.Iterations[index].Enclosure.Contains(previous),
                Inv($"Iteration {index} still admitted the previous answer {previous}."));

            Assert.True(
                run.Iterations[index].Simplest.Value.Denominator > run.Iterations[index - 1].Simplest.Value.Denominator,
                Inv($"Iteration {index} did not climb past denominator {previous.Denominator}."));
        }
    }

    [Fact]
    public void OutOfReachControl_FindsNothingAndEveryRowStaysAwayFromZero()
    {
        ConstantRun run = Execute(new HalvingConstant(OutOfReach), OutOfReachSchedule());

        foreach (ConstantIteration iteration in run.Iterations)
        {
            Assert.NotEqual(OutOfReach, iteration.Simplest.Value);
        }

        // A row settles at its candidate's true distance from the constant, which is non-zero for
        // every candidate that is not the constant. What the run earns is a denominator bound.
        foreach (TrendRow row in run.Matrix.Rows)
        {
            Assert.True(
                row.Distances[^1].Sign > 0,
                Inv($"The row for {row.Candidate} reached zero, which would be a find."));
        }
    }

    // ---------- control three: the near miss ----------

    [Fact]
    public void NearMissControl_ReportsTheSimpleRationalAtEveryIterationWhileNarrowing()
    {
        // The exhibit in miniature. The limit is 10^-30 above 6, so every enclosure this schedule
        // reaches contains 6 and the sweep stops at denominator 1 every time. The run is not
        // stalled - MaxError falls by a factor of 16 per column - so the constant answer is
        // evidence about the target's arithmetic rather than about the pipeline.
        BigRational[] schedule =
        [
            PowerOfTwo(-4), PowerOfTwo(-8), PowerOfTwo(-12), PowerOfTwo(-16), PowerOfTwo(-20),
        ];

        ConstantRun run = Execute(new HalvingConstant(NearMiss), schedule);

        foreach (ConstantIteration iteration in run.Iterations)
        {
            Assert.Equal(Ratio(6, 1), iteration.Simplest.Value);
            Assert.Equal(BigInteger.One, iteration.Simplest.Value.Denominator);
        }

        for (int index = 1; index < run.Iterations.Count; index++)
        {
            Assert.True(
                run.Iterations[index].Enclosure.MaxError < run.Iterations[index - 1].Enclosure.MaxError,
                "The enclosure stopped narrowing, so the constant answer would prove nothing.");
        }

        // And this is what makes it an exhibit rather than a curiosity: the row for 6 *falls*, at
        // every iteration, exactly as a genuine find would. It floors at 10^-30 and this schedule
        // never gets there, so nothing visible here distinguishes the two.
        TrendRow row = RowFor(run, Ratio(6, 1));
        Assert.Single(run.Matrix.Rows);
        AssertStrictlyFalling(row);
        Assert.True(row.Distances[^1].Sign > 0, "The row reached zero, so it would not be a near miss.");
    }

    // ---------- the guards, on paths that skip the search ----------

    [Fact]
    public void ScheduleGuard_EveryControlStaysInsideItsMeasuredDepthBudget()
    {
        // Pays for refinement only - no sweep - so a schedule deepened past what was measured
        // reddens in milliseconds instead of running for an unbounded time. Naming a rational the
        // final enclosure already contains bounds the sweep's depth from above without running it:
        // the sweep stops at the least enclosed denominator, so it cannot go past this one.
        (BigRational Limit, BigRational[] Schedule, BigRational Budget, int Denominator)[] controls =
        [
            (KnownAnswer, [PowerOfTwo(-2), PowerOfTwo(-20)], KnownAnswer, 113),
            (OutOfReach, OutOfReachSchedule(), Ratio(987, 1597), 1597),
            (NearMiss, [PowerOfTwo(-4), PowerOfTwo(-20)], Ratio(6, 1), 1),
        ];

        foreach ((BigRational limit, BigRational[] schedule, BigRational budget, int denominator) in controls)
        {
            Approximation final = FinalEnclosure(new HalvingConstant(limit), schedule);

            Assert.Equal(schedule[^1], final.MaxError);
            Assert.True(
                final.Contains(budget),
                Inv($"The limit {limit} no longer admits {budget}, so the sweep will pass denominator {denominator}."));
        }
    }

    [Fact]
    public void Execute_WithAnApproximatorThatNeverTerminates_IsRefusedRatherThanRun()
    {
        // A defective search fails by running forever, which no ordinary assertion catches: the
        // test reports nothing, indefinitely. The budget turns that into a failure.
        Assert.Throws<InvalidOperationException>(
            () => ConstantRun.Execute(
                new HalvingConstant(KnownAnswer),
                [PowerOfTwo(-4)],
                BoundedSearch.Budgeted(new EndlessApproximator())));
    }

    // ---------- refusals ----------

    [Fact]
    public void Execute_RejectsANullConstantOrSchedule()
    {
        Assert.Throws<ArgumentNullException>(() => ConstantRun.Execute(null!, [PowerOfTwo(-2)]));
        Assert.Throws<ArgumentNullException>(() => ConstantRun.Execute(new HalvingConstant(), null!));
    }

    [Fact]
    public void Execute_RejectsAScheduleThatDoesNotStrictlyDecrease()
    {
        var constant = new HalvingConstant(KnownAnswer);

        Assert.Throws<ArgumentException>(
            () => Execute(constant, [PowerOfTwo(-4), PowerOfTwo(-4)]));
        Assert.Throws<ArgumentException>(
            () => Execute(constant, [PowerOfTwo(-8), PowerOfTwo(-4)]));
    }

    [Fact]
    public void Execute_RejectsANonPositiveFinalTarget()
    {
        var constant = new HalvingConstant(KnownAnswer);

        Assert.Throws<ArgumentException>(() => Execute(constant, [BigRational.Zero]));
        Assert.Throws<ArgumentException>(() => Execute(constant, [PowerOfTwo(-2), Ratio(-1, 4)]));
    }

    [Fact]
    public void Execute_ReportsAProviderWhoseRefinementsEnd() =>
        Assert.Throws<InvalidOperationException>(
            () => Execute(new FiniteConstant(), [PowerOfTwo(-40)]));

    // ---------- fixtures ----------

    /// <summary>Runs against a budgeted sweep, so no test in this class can hang on a defective search.</summary>
    private static ConstantRun Execute(IRealConstant constant, BigRational[] schedule) =>
        ConstantRun.Execute(constant, schedule, BoundedSearch.Budgeted(new DenominatorSweep()));

    /// <summary>
    /// The out-of-reach schedule, chosen by measuring three: this one advances at every step under
    /// both a one-sided and a centred enclosure, where the neighbouring uniform schedule repeats a
    /// candidate and so cannot carry the exclusion criterion.
    /// </summary>
    private static BigRational[] OutOfReachSchedule() =>
        [PowerOfTwo(-10), PowerOfTwo(-14), PowerOfTwo(-16), PowerOfTwo(-18), PowerOfTwo(-22)];

    /// <summary>The ratio of two consecutive Fibonacci numbers, in lowest terms since they are coprime.</summary>
    private static BigRational FibonacciRatio(int index)
    {
        BigInteger previous = BigInteger.Zero;
        BigInteger current = BigInteger.One;

        for (int i = 0; i < index; i++)
        {
            (previous, current) = (current, previous + current);
        }

        return new BigRational(previous, current);
    }

    /// <summary>
    /// Drives a provider to the last target of a schedule using one held enumerator, paying for
    /// refinement and nothing else.
    /// </summary>
    private static Approximation FinalEnclosure(HalvingConstant constant, BigRational[] schedule)
    {
        using IEnumerator<Approximation> refinements = constant.Refinements().GetEnumerator();

        Approximation current = default;
        bool started = false;

        foreach (BigRational target in schedule)
        {
            while (!started || current.MaxError > target)
            {
                Assert.True(refinements.MoveNext(), "The provider's refinements ended.");
                current = refinements.Current;
                started = true;
            }
        }

        return current;
    }

    private static TrendRow RowFor(ConstantRun run, BigRational candidate) =>
        run.Matrix.Rows.Single(row => row.Candidate == candidate);

    private static void AssertStrictlyFalling(TrendRow row)
    {
        for (int column = 1; column < row.Distances.Count; column++)
        {
            Assert.True(
                row.Distances[column] < row.Distances[column - 1],
                Inv($"The row for {row.Candidate} did not fall at column {column}."));
        }
    }
}

/// <summary>
/// A deliberately defective approximator that yields improvements without end and never encloses.
/// </summary>
/// <remarks>
/// Its defect's symptom is non-termination rather than a wrong answer, which is exactly the shape
/// nothing routine catches - a mutation run reports red tests, this one reports nothing.
/// </remarks>
internal sealed class EndlessApproximator : IRationalApproximator
{
    public IEnumerable<RationalCandidate> Search(Approximation enclosure)
    {
        // Each candidate is strictly closer than the last and none is ever enclosed, so the
        // improvement filter never stops it.
        BigRational offset = BigRational.One;

        while (true)
        {
            offset *= TestConstants.Half;
            yield return RationalCandidate.Against(
                enclosure.Value + enclosure.MaxError + enclosure.MaxError + offset, enclosure);
        }
    }
}
