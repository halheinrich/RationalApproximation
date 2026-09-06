using static HalHeinrich.Numerics.Tests.Sampling;
using static HalHeinrich.Numerics.Tests.TestConstants;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// <see cref="AffineConstant"/>: the exactness of its bound, the edges that would lose it, and the
/// obligations it inherits from the constant it wraps.
/// </summary>
/// <remarks>
/// The claim under test is an <b>equality</b>, not an inequality - the bound is exactly
/// <c>|scale|</c> times the inner's - so these tests assert equality where the rest of the library
/// asserts containment, and separately try to violate the bound by mapping every point the inner
/// enclosure permits through the affine map.
/// </remarks>
public class AffineConstantTests
{
    /// <summary>The scale of the near-miss exhibit, and the case where a careless bound loses everything.</summary>
    private static BigRational Tiny { get; } = BigRational.Pow(Ratio(1, 10), 30);

    // ---------- construction ----------

    [Fact]
    public void Constructor_RejectsAZeroScale()
    {
        // Not defensiveness: a zero scale makes every refinement the exact value offset, identical
        // to its predecessor, which no inner constant could rescue.
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AffineConstant(Ratio(6, 1), BigRational.Zero, new HalvingConstant()));

        Assert.Contains("strictly-improving", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsANullInner() =>
        Assert.Throws<ArgumentNullException>(
            () => new AffineConstant(BigRational.Zero, BigRational.One, null!));

    [Fact]
    public void Constructor_AcceptsTheEdgesThatAreLegal()
    {
        // A zero offset is a pure scaling; a negative scale is a reflection. Both are ordinary.
        var scaling = new AffineConstant(BigRational.Zero, Ratio(7, 2), new HalvingConstant());
        var reflection = new AffineConstant(Ratio(6, 1), Ratio(-1, 3), new HalvingConstant());

        Assert.Equal(BigRational.Zero, scaling.Offset);
        Assert.Equal(Ratio(7, 2), scaling.Scale);
        Assert.Equal(Ratio(-1, 3), reflection.Scale);
    }

    // ---------- ErrorBoundAt ----------

    [Fact]
    public void ErrorBoundAt_IsTheMagnitudeOfTheScaleTimesTheInnerBound()
    {
        foreach (IRealConstant inner in new IRealConstant[] { new HalvingConstant(), new PlateauConstant() })
        {
            foreach (BigRational scale in new[] { BigRational.One, Ratio(-1, 1), Ratio(7, 2), Ratio(-5, 3), Tiny })
            {
                var affine = new AffineConstant(Ratio(6, 1), scale, inner);

                for (int step = 0; step <= 40; step++)
                {
                    Assert.Equal(
                        BigRational.Abs(scale) * inner.ErrorBoundAt(step),
                        affine.ErrorBoundAt(step));
                }
            }
        }
    }

    [Fact]
    public void ErrorBoundAt_UpperBoundsTheRealisedErrorOfTheSameStep()
    {
        // The two are computed by different routes - the bound multiplies the inner's planned
        // bound, the refinement runs through Approximation.Multiply - so their agreement is a
        // claim rather than a restatement.
        bool sawAStrictGap = false;

        foreach (IRealConstant inner in new IRealConstant[] { new HalvingConstant(), new PlateauConstant() })
        {
            foreach (BigRational scale in new[] { Ratio(7, 2), Ratio(-5, 3), Tiny })
            {
                var affine = new AffineConstant(Ratio(-4, 1), scale, inner);

                int step = 0;
                foreach (Approximation refinement in affine.Refinements().Take(30))
                {
                    BigRational bound = affine.ErrorBoundAt(step);

                    Assert.True(
                        refinement.MaxError <= bound,
                        Inv($"Step {step} realised {refinement.MaxError} against a bound of {bound}."));

                    sawAStrictGap |= refinement.MaxError < bound;
                    step++;
                }
            }
        }

        // Without this the test would pass on a bound that merely equalled the realised error
        // everywhere, which would not exercise the inequality at all.
        Assert.True(sawAStrictGap, "No step had a bound strictly above its realised error.");
    }

    [Fact]
    public void ErrorBoundAt_RejectsANegativeStep()
    {
        var affine = new AffineConstant(BigRational.Zero, Ratio(3, 1), new HalvingConstant());

        Assert.Throws<ArgumentOutOfRangeException>(() => affine.ErrorBoundAt(-1));
    }

    [Fact]
    public void ErrorBoundAt_IsNonIncreasingAndTendsToZero()
    {
        // Both survive multiplication by a fixed positive rational; tending to zero is the one
        // that needs the scale to be non-zero, which is the second place that refusal earns itself.
        foreach (BigRational scale in new[] { Ratio(7, 2), Ratio(-5, 3), Tiny })
        {
            var affine = new AffineConstant(Ratio(6, 1), scale, new PlateauConstant());

            BigRational previous = affine.ErrorBoundAt(0);
            for (int step = 1; step <= 60; step++)
            {
                BigRational current = affine.ErrorBoundAt(step);
                Assert.True(current <= previous, Inv($"Bound rose at step {step}: {previous} then {current}."));
                previous = current;
            }

            // PlateauConstant's bound is flat over runs of three, so step 700 is 2^-233, and even
            // the largest scale here leaves that far below 10^-60.
            Assert.True(
                affine.ErrorBoundAt(700) < Tiny * Tiny,
                Inv($"Scale {scale} left the bound at {affine.ErrorBoundAt(700)} by step 700."));
        }
    }

    // ---------- the bound, asserted as an equality and then attacked ----------

    [Fact]
    public void Refinements_CarryTheExactAffineValueAndAnExactlyScaledBound()
    {
        foreach (IRealConstant inner in new IRealConstant[] { new HalvingConstant(), new PlateauConstant() })
        {
            foreach ((BigRational offset, BigRational scale) in AffineMaps())
            {
                var affine = new AffineConstant(offset, scale, inner);

                List<Approximation> innerSteps = [.. inner.Refinements().Take(12)];
                List<Approximation> affineSteps = [.. affine.Refinements().Take(12)];

                for (int step = 0; step < innerSteps.Count; step++)
                {
                    Assert.Equal(offset + (scale * innerSteps[step].Value), affineSteps[step].Value);

                    // Neither widened nor narrowed. Nothing rounds here, and a reader arriving from
                    // the rest of the library will be looking for the widening step.
                    Assert.Equal(
                        BigRational.Abs(scale) * innerSteps[step].MaxError,
                        affineSteps[step].MaxError);
                }
            }
        }
    }

    [Fact]
    public void TheBound_HoldsForEveryValueTheInnerEnclosurePermits()
    {
        // A bound is tested by trying to violate it. Every point of the inner's closed interval is
        // a value the truth is permitted to take, so its affine image must lie inside the affine
        // enclosure - endpoints included, where a too-tight bound fails first.
        IRealConstant[] inners =
        [
            new HalvingConstant(),
            new PlateauConstant(),
            new AffineConstant(Ratio(-3, 1), Ratio(2, 1), new HalvingConstant()),
        ];

        foreach (IRealConstant inner in inners)
        {
            foreach ((BigRational offset, BigRational scale) in AffineMaps())
            {
                var affine = new AffineConstant(offset, scale, inner);

                List<Approximation> innerSteps = [.. inner.Refinements().Take(9)];
                List<Approximation> affineSteps = [.. affine.Refinements().Take(9)];

                for (int step = 0; step < innerSteps.Count; step++)
                {
                    foreach (BigRational point in PointsOf(innerSteps[step]))
                    {
                        BigRational image = offset + (scale * point);

                        Assert.True(
                            affineSteps[step].Contains(image),
                            Inv($"Step {step} of {offset} + {scale}*inner excluded the image {image} of a permitted {point}."));
                    }
                }
            }
        }
    }

    [Fact]
    public void Refinements_EncloseTheAffineTruthAndImproveStrictly()
    {
        foreach ((BigRational offset, BigRational scale) in AffineMaps())
        {
            var affine = new AffineConstant(offset, scale, new HalvingConstant());
            BigRational truth = offset + (scale * HalvingConstant.Truth);

            BigRational? previous = null;
            foreach (Approximation refinement in affine.Refinements().Take(40))
            {
                Assert.True(refinement.Contains(truth), Inv($"A refinement stopped enclosing {truth}."));

                if (previous is BigRational earlier)
                {
                    Assert.True(
                        refinement.MaxError < earlier,
                        Inv($"MaxError did not improve: {earlier} then {refinement.MaxError}."));
                }

                previous = refinement.MaxError;
            }
        }
    }

    // ---------- the edges named in the design ----------

    [Fact]
    public void ANegativeScale_ReflectsTheValueAndLeavesThePositiveBound()
    {
        var inner = new HalvingConstant();
        var reflected = new AffineConstant(BigRational.Zero, Ratio(-1, 1), inner);

        List<Approximation> innerSteps = [.. inner.Refinements().Take(10)];
        List<Approximation> reflectedSteps = [.. reflected.Refinements().Take(10)];

        for (int step = 0; step < innerSteps.Count; step++)
        {
            Assert.Equal(-innerSteps[step].Value, reflectedSteps[step].Value);
            Assert.Equal(innerSteps[step].MaxError, reflectedSteps[step].MaxError);
            Assert.True(reflectedSteps[step].MaxError.Sign >= 0, "A reflection produced a negative radius.");
        }
    }

    [Fact]
    public void AZeroOffset_IsAPureScaling()
    {
        var inner = new HalvingConstant();
        var scaled = new AffineConstant(BigRational.Zero, Ratio(7, 2), inner);

        List<Approximation> innerSteps = [.. inner.Refinements().Take(10)];
        List<Approximation> scaledSteps = [.. scaled.Refinements().Take(10)];

        for (int step = 0; step < innerSteps.Count; step++)
        {
            Assert.Equal(Ratio(7, 2) * innerSteps[step].Value, scaledSteps[step].Value);
            Assert.Equal(Ratio(7, 2) * innerSteps[step].MaxError, scaledSteps[step].MaxError);
        }
    }

    [Fact]
    public void AnAffineConstantWrappingAnother_ComposesWithoutLoss()
    {
        // Nesting is again affine, and because neither layer rounds the composed bound is exactly
        // the product of the two magnitudes times the innermost bound.
        var innermost = new HalvingConstant();
        var middle = new AffineConstant(Ratio(-2, 1), Ratio(5, 1), innermost);
        var outer = new AffineConstant(BigRational.One, Ratio(3, 1), middle);

        List<Approximation> innermostSteps = [.. innermost.Refinements().Take(12)];
        List<Approximation> outerSteps = [.. outer.Refinements().Take(12)];

        // 1 + 3*(-2 + 5*inner) = -5 + 15*inner, and the truth is 1 + 3*(-2 + 5*1) = 10.
        for (int step = 0; step < innermostSteps.Count; step++)
        {
            Assert.Equal(Ratio(-5, 1) + (Ratio(15, 1) * innermostSteps[step].Value), outerSteps[step].Value);
            Assert.Equal(Ratio(15, 1) * innermostSteps[step].MaxError, outerSteps[step].MaxError);
            Assert.True(outerSteps[step].Contains(Ratio(10, 1)));
        }
    }

    [Fact]
    public void AScaleOfOneOverTenToTheThirty_KeepsTheWholeBound()
    {
        // The exhibit's own scale, and the case a careless bound loses: multiplying a bound by
        // 10^-30 must scale it, not flatten it to zero and not leave it at the inner's size.
        var inner = new HalvingConstant();
        var affine = new AffineConstant(Ratio(6, 1) - Tiny, Tiny, inner);

        List<Approximation> innerSteps = [.. inner.Refinements().Take(40)];
        List<Approximation> affineSteps = [.. affine.Refinements().Take(40)];

        for (int step = 0; step < innerSteps.Count; step++)
        {
            Assert.Equal(Tiny * innerSteps[step].MaxError, affineSteps[step].MaxError);
            Assert.False(affineSteps[step].IsExact, Inv($"Step {step} collapsed to an exact enclosure."));

            // 6 - 10^-30 + 10^-30 * 1 is exactly 6: the whole point of the exhibit is that a value
            // this close to a simple rational is still enclosed by every refinement.
            Assert.True(affineSteps[step].Contains(Ratio(6, 1)), Inv($"Step {step} stopped enclosing 6."));
        }

        Assert.Equal(Tiny * PowerOfTwo(-39), affineSteps[^1].MaxError);
    }

    // ---------- the defaulted members, one delegated and one not ----------

    [Fact]
    public void StepFor_AgreesWithAnExhaustiveScanOverItsOwnBound()
    {
        // The delegation is the thing under test, so the expectation is a scan built from the
        // definition against the affine constant's own bound - never against the inner's.
        foreach (BigRational scale in new[] { Ratio(4, 1), Ratio(-4, 1), Ratio(1, 5), Tiny })
        {
            var affine = new AffineConstant(Ratio(6, 1), scale, new HalvingConstant());

            for (int denominator = 1; denominator <= 120; denominator++)
            {
                BigRational target = Ratio(1, denominator);
                Assert.Equal(ScanForStep(affine, target), affine.StepFor(target));
            }
        }
    }

    [Fact]
    public void StepFor_IsReachableOnTheConcreteTypeAndAgreesWithTheInterface()
    {
        // Re-declared, so unlike ApproximateTo it does not need an IRealConstant-typed reference.
        var affine = new AffineConstant(Ratio(6, 1), Ratio(4, 1), new HalvingConstant());

        Assert.Equal(ScanForStep(affine, Ratio(1, 7)), affine.StepFor(Ratio(1, 7)));
        Assert.Equal(affine.StepFor(Ratio(1, 7)), ((IRealConstant)affine).StepFor(Ratio(1, 7)));
    }

    [Fact]
    public void StepFor_CostsNoRefinements()
    {
        var counting = new CountingConstant(new HalvingConstant());
        var affine = new AffineConstant(Ratio(6, 1), Ratio(4, 1), counting);

        int step = affine.StepFor(PowerOfTwo(-1000));

        Assert.Equal(1002, step);
        Assert.Equal(0, counting.SequencesStarted);
        Assert.Equal(0, counting.RefinementsPulled);
    }

    [Fact]
    public void StepFor_RejectsANonPositiveTarget()
    {
        // Rejected by the inner's own check. Dividing by a strictly positive magnitude preserves
        // the sign, so the division cannot mask a bad target on the way down.
        var affine = new AffineConstant(Ratio(6, 1), Ratio(-4, 1), new HalvingConstant());

        Assert.Throws<ArgumentOutOfRangeException>(() => affine.StepFor(BigRational.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => affine.StepFor(Ratio(-1, 4)));
    }

    [Fact]
    public void ApproximateTo_ThroughTheInterface_MeetsTheTargetAndEnclosesTheTruth()
    {
        var affine = new AffineConstant(Ratio(6, 1), Ratio(-4, 1), new HalvingConstant());
        BigRational truth = Ratio(6, 1) + (Ratio(-4, 1) * HalvingConstant.Truth);

        foreach (int denominator in new[] { 2, 3, 97, 1000, 65536 })
        {
            BigRational target = Ratio(1, denominator);
            Approximation result = ((IRealConstant)affine).ApproximateTo(target);

            Assert.True(result.MaxError <= target, Inv($"MaxError {result.MaxError} missed target {target}."));
            Assert.True(result.Contains(truth), Inv($"The result stopped enclosing {truth} at target {target}."));
        }
    }

    // ---------- laziness and incrementality, which no return value shows ----------

    [Fact]
    public void Refinements_PullNothingUntilEnumeratedAndThenOnePerElement()
    {
        var counting = new CountingConstant(new HalvingConstant());
        var affine = new AffineConstant(Ratio(6, 1), Ratio(4, 1), counting);

        IEnumerable<Approximation> sequence = affine.Refinements();
        Assert.Equal(0, counting.SequencesStarted);
        Assert.Equal(0, counting.RefinementsPulled);

        List<Approximation> taken = [.. sequence.Take(7)];

        Assert.Equal(7, taken.Count);
        Assert.Equal(1, counting.SequencesStarted);
        Assert.Equal(7, counting.RefinementsPulled);
    }

    [Fact]
    public void Refinements_StartAFreshInnerSequenceOnEachCall()
    {
        var counting = new CountingConstant(new HalvingConstant());
        var affine = new AffineConstant(Ratio(6, 1), Ratio(4, 1), counting);

        List<Approximation> first = [.. affine.Refinements().Take(5)];
        List<Approximation> second = [.. affine.Refinements().Take(5)];

        Assert.Equal(first, second);
        Assert.Equal(2, counting.SequencesStarted);
        Assert.Equal(10, counting.RefinementsPulled);
    }

    /// <summary>
    /// The affine maps the assertions are swept over: identity, reflection, pure scaling, a
    /// negative offset with a negative scale, and the exhibit's own shape.
    /// </summary>
    private static (BigRational Offset, BigRational Scale)[] AffineMaps() =>
    [
        (BigRational.Zero, BigRational.One),
        (BigRational.Zero, Ratio(-1, 1)),
        (BigRational.Zero, Ratio(7, 2)),
        (Ratio(-7, 3), Ratio(-5, 2)),
        (Ratio(6, 1), Ratio(1, 2)),
        (Ratio(6, 1) - Tiny, Tiny),
    ];
}
