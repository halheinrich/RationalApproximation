using System.Numerics;
using System.Reflection;

using static HalHeinrich.Numerics.Tests.Sampling;

namespace HalHeinrich.Numerics.Tests;

/// <summary>
/// The trend matrix: its mechanics, its dense fill, the exactness of its cells, and the two shapes
/// a row can take.
/// </summary>
/// <remarks>
/// Every fixture here is a hand-built sequence of enclosures. That is enough to prove what the
/// matrix does with what it is given, and it is <b>not</b> enough to demonstrate the behaviour
/// that justifies the matrix existing - a real candidate holding steady across iterations and then
/// moving on. That needs real providers and is step 5's obligation, alongside the spec's controls.
/// Tests here that build a plateau by hand say so on the tin.
/// </remarks>
public class TrendMatrixTests
{
    // ---------- fixtures ----------

    /// <summary>An exact enclosure, since these fixtures are about the distances and not the bounds.</summary>
    private static TrendIteration IterationAt(BigRational ratio, params BigRational[] candidates)
    {
        Approximation enclosure = Approximation.Exact(ratio);
        return TrendIteration.Of(enclosure, candidates.Select(c => RationalCandidate.Against(c, enclosure)));
    }

    /// <summary>
    /// A run whose ratio closes on exactly 6, the spec's positive-control target for pi^2/zeta(2).
    /// The distance from 6 is 1/4^(k+1), so it falls; the distance from 26 is 20 - 1/4^(k+1), so
    /// it climbs to 20 and settles.
    /// </summary>
    private static List<TrendIteration> ClosingOnSix(int iterations)
    {
        var run = new List<TrendIteration>();
        BigRational offset = Ratio(1, 4);

        for (int k = 0; k < iterations; k++)
        {
            run.Add(k == 0
                ? IterationAt(Ratio(6, 1) + offset, Ratio(6, 1), Ratio(26, 1))
                : IterationAt(Ratio(6, 1) + offset));

            offset /= Ratio(4, 1);
        }

        return run;
    }

    // ---------- mechanics ----------

    [Fact]
    public void Build_HasOneColumnPerIterationAndOneRowPerDistinctCandidate()
    {
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(5, 1), Ratio(5, 1)),
            IterationAt(Ratio(11, 2), Ratio(11, 2), Ratio(5, 1)),
            IterationAt(Ratio(23, 4), Ratio(23, 4)),
        ]);

        Assert.Equal(3, matrix.Ratios.Count);
        Assert.Equal(3, matrix.Rows.Count);
        Assert.All(matrix.Rows, row => Assert.Equal(3, row.Distances.Count));
    }

    [Fact]
    public void Build_CellsAreTheExactDistanceToEachIterationsValue()
    {
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(5, 1), Ratio(6, 1)),
            IterationAt(Ratio(25, 4)),
            IterationAt(Ratio(6, 1)),
        ]);

        TrendRow row = Assert.Single(matrix.Rows);

        // |6 - 5| = 1, |6 - 25/4| = 1/4, |6 - 6| = 0. Exact, and unsigned.
        Assert.Equal(new[] { Ratio(1, 1), Ratio(1, 4), BigRational.Zero }, row.Distances);
    }

    [Fact]
    public void Build_MeasuresCellsFromTheValueNotFromAnEndpoint()
    {
        // Every other fixture here uses an exact enclosure, where Value, Lower and Upper coincide,
        // so only an inexact one can tell them apart at all. Section 2 names the value.
        Approximation first = Approximation.Create(Ratio(6, 1), Ratio(1, 1));
        Approximation second = Approximation.Create(Ratio(13, 2), Ratio(1, 4));

        TrendMatrix matrix = TrendMatrix.Build(
        [
            TrendIteration.Of(first, [RationalCandidate.Against(Ratio(10, 1), first)]),
            TrendIteration.Of(second, []),
        ]);

        TrendRow row = Assert.Single(matrix.Rows);

        // |10 - 6| = 4 and |10 - 13/2| = 7/2. Measured from the lower endpoints they would have
        // been 5 and 15/4; from the upper ones, 3 and 13/4.
        Assert.Equal(new[] { Ratio(4, 1), Ratio(7, 2) }, row.Distances);
    }

    [Fact]
    public void Build_FillsEveryColumnForACandidateFirstSeenLate()
    {
        // The density requirement. A candidate that only surfaced at iteration 3 still has cells
        // for 0, 1 and 2, because the distance to an earlier ratio needs nothing but the candidate
        // and that ratio. A sparse row would hide the history the trend is read from.
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(1, 1)),
            IterationAt(Ratio(2, 1)),
            IterationAt(Ratio(3, 1)),
            IterationAt(Ratio(4, 1), Ratio(10, 1)),
        ]);

        TrendRow row = Assert.Single(matrix.Rows);

        Assert.Equal(3, row.FirstSeenAt);
        Assert.Equal(4, row.Distances.Count);
        Assert.Equal(new[] { Ratio(9, 1), Ratio(8, 1), Ratio(7, 1), Ratio(6, 1) }, row.Distances);
    }

    [Fact]
    public void Build_LetsAControlBeWatchedByNamingItOnce()
    {
        // A consequence of density worth relying on: a rational no search produced gets a full row
        // from being named in a single iteration. That is how the spec's positive controls are
        // watched without inventing a provider for them.
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(13, 2), Ratio(6, 1)),
            IterationAt(Ratio(49, 8)),
            IterationAt(Ratio(97, 16)),
        ]);

        TrendRow control = Assert.Single(matrix.Rows);

        Assert.Equal(Ratio(6, 1), control.Candidate);
        Assert.Equal(new[] { Ratio(1, 2), Ratio(1, 8), Ratio(1, 16) }, control.Distances);
    }

    [Fact]
    public void Build_GivesARepeatedCandidateOneRowRecordingTheEarliestIteration()
    {
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(5, 1)),
            IterationAt(Ratio(11, 2), Ratio(6, 1)),
            IterationAt(Ratio(23, 4), Ratio(6, 1)),
            IterationAt(Ratio(47, 8), Ratio(6, 1)),
        ]);

        TrendRow row = Assert.Single(matrix.Rows);

        Assert.Equal(1, row.FirstSeenAt);
    }

    [Fact]
    public void Build_KeepsTheWholeEnclosureForEachColumn()
    {
        // The value alone would be enough for the cells; the bound is what tells a reader how much
        // of an apparent plateau the evidence actually supports.
        Approximation wide = Approximation.Create(Ratio(6, 1), Ratio(1, 2));
        Approximation narrow = Approximation.Create(Ratio(6, 1), Ratio(1, 1000));

        TrendMatrix matrix = TrendMatrix.Build(
        [
            TrendIteration.Of(wide, [RationalCandidate.Against(Ratio(6, 1), wide)]),
            TrendIteration.Of(narrow, []),
        ]);

        Assert.Equal(Ratio(1, 2), matrix.Ratios[0].MaxError);
        Assert.Equal(Ratio(1, 1000), matrix.Ratios[1].MaxError);
    }

    // ---------- row order ----------

    [Fact]
    public void Build_OrdersRowsByHeightThenValue()
    {
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(6, 1), Ratio(26, 1), Ratio(6, 1), Ratio(-6, 1), Ratio(13, 2), Ratio(1, 1)),
        ]);

        Assert.Equal(
            new[] { Ratio(1, 1), Ratio(-6, 1), Ratio(6, 1), Ratio(13, 2), Ratio(26, 1) },
            matrix.Rows.Select(r => r.Candidate));

        Assert.Equal(
            new[] { BigInteger.One, new BigInteger(6), new BigInteger(6), new BigInteger(13), new BigInteger(26) },
            matrix.Rows.Select(r => r.Height));
    }

    [Fact]
    public void Build_RowOrderIsUnchangedWhenTheRunIsExtended()
    {
        // Two runs of a growing experiment should line up row for row, so the order cannot depend
        // on which iteration happened to surface a candidate.
        List<TrendIteration> shortRun = ClosingOnSix(3);
        List<TrendIteration> longRun = ClosingOnSix(8);

        TrendMatrix first = TrendMatrix.Build(shortRun);
        TrendMatrix second = TrendMatrix.Build(longRun);

        Assert.Equal(first.Rows.Select(r => r.Candidate), second.Rows.Select(r => r.Candidate));
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        List<TrendIteration> run = ClosingOnSix(6);

        TrendMatrix first = TrendMatrix.Build(run);
        TrendMatrix second = TrendMatrix.Build(run);

        Assert.Equal(first.Rows.Select(r => r.Candidate), second.Rows.Select(r => r.Candidate));
        for (int i = 0; i < first.Rows.Count; i++)
        {
            Assert.Equal(first.Rows[i].Distances, second.Rows[i].Distances);
        }
    }

    // ---------- the two shapes ----------

    [Fact]
    public void Rows_ShowAVanishingShapeAndAPlateauShape()
    {
        // The ratio closes on exactly 6, so 6/1's row falls towards zero and 26/1's climbs to
        // |26 - 6| = 20 and settles there. This is the matrix doing its job on a sequence built to
        // have that shape; it is not evidence that any real provider behaves this way.
        TrendMatrix matrix = TrendMatrix.Build(ClosingOnSix(10));

        TrendRow six = matrix.Rows.Single(r => r.Candidate == Ratio(6, 1));
        TrendRow twentySix = matrix.Rows.Single(r => r.Candidate == Ratio(26, 1));

        for (int k = 1; k < matrix.Ratios.Count; k++)
        {
            Assert.True(six.Distances[k] < six.Distances[k - 1], Inv($"The vanishing row rose at {k}."));
            Assert.True(twentySix.Distances[k] > twentySix.Distances[k - 1], Inv($"The settling row fell at {k}."));
        }

        Assert.True(six.Distances[^1] < Ratio(1, 100000));

        // Every cell of the settling row is strictly below its limit, and the last is very close.
        Assert.All(twentySix.Distances, d => Assert.True(d < Ratio(20, 1)));
        Assert.True(twentySix.Distances[^1] > Ratio(20, 1) - Ratio(1, 100000));
    }

    [Fact]
    public void Rows_RecordAnExactZeroWhenAnIterationLandsOnTheCandidate()
    {
        // "A row falling to zero is the answer" is a statement about a limit, but a cell can be
        // exactly zero when an iteration's value happens to be the candidate. Nothing here treats
        // that as a conclusion.
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(5, 1), Ratio(6, 1)),
            IterationAt(Ratio(6, 1)),
            IterationAt(Ratio(97, 16)),
        ]);

        TrendRow row = Assert.Single(matrix.Rows);

        Assert.Equal(BigRational.Zero, row.Distances[1]);
        Assert.NotEqual(BigRational.Zero, row.Distances[2]);
    }

    [Fact]
    public void Rows_RecordAHeldValueWithoutJudgingIt()
    {
        // Hand-built, and only a demonstration that the matrix RECORDS a candidate holding steady
        // and then moving on. The spec observed that shape in real runs - 5/31 in a control and
        // 97525 in the target - and it is why no "unchanged for k rounds" rule may be added here.
        // Reproducing it from real providers is step 5's obligation, not this fixture's claim.
        TrendMatrix matrix = TrendMatrix.Build(
        [
            IterationAt(Ratio(5, 1), Ratio(6, 1)),
            IterationAt(Ratio(7, 1)),
            IterationAt(Ratio(7, 1)),
            IterationAt(Ratio(13, 2)),
        ]);

        TrendRow row = Assert.Single(matrix.Rows);

        Assert.Equal(Ratio(1, 1), row.Distances[0]);
        Assert.Equal(Ratio(1, 1), row.Distances[1]);
        Assert.Equal(Ratio(1, 1), row.Distances[2]);
        Assert.Equal(Ratio(1, 2), row.Distances[3]);
    }

    // ---------- the ruling, encoded ----------

    [Fact]
    public void TrendTypes_ExposeNoVerdict()
    {
        // No IsConverged, no Answer, no HasPlateaued. Each would be the rejected stopping rule
        // wearing a property name, and the first caller to find one would read it as a verdict.
        // A boolean on either type would be that same thing under any name, so the shape is
        // barred as well as the vocabulary. If a genuinely descriptive boolean is ever wanted,
        // this test is where the argument has to be made.
        string[] barred = ["converge", "answer", "plateau", "verdict", "winner", "settled", "stable", "best"];

        foreach (Type type in new[] { typeof(TrendMatrix), typeof(TrendRow), typeof(TrendIteration) })
        {
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (string word in barred)
                {
                    Assert.False(
                        member.Name.Contains(word, StringComparison.OrdinalIgnoreCase),
                        Inv($"{type.Name}.{member.Name} reads as a verdict."));
                }

                Type? returned = member switch
                {
                    PropertyInfo property => property.PropertyType,
                    MethodInfo method => method.ReturnType,
                    _ => null,
                };

                Assert.True(
                    returned != typeof(bool),
                    Inv($"{type.Name}.{member.Name} returns a boolean, which on these types is a verdict."));
            }
        }
    }

    // ---------- edges ----------

    [Fact]
    public void Build_OnARunWithNoIterations_IsEmpty()
    {
        TrendMatrix matrix = TrendMatrix.Build([]);

        Assert.Empty(matrix.Ratios);
        Assert.Empty(matrix.Rows);
    }

    [Fact]
    public void Build_OnIterationsWithNoCandidates_HasColumnsButNoRows()
    {
        TrendMatrix matrix = TrendMatrix.Build([IterationAt(Ratio(6, 1)), IterationAt(Ratio(13, 2))]);

        Assert.Equal(2, matrix.Ratios.Count);
        Assert.Empty(matrix.Rows);
    }

    [Fact]
    public void Build_RejectsANullRun()
    {
        // The parameter name is asserted, not just the exception type: without an explicit guard
        // the spread inside Build would still throw, but from somewhere the caller cannot act on.
        ArgumentNullException thrown = Assert.Throws<ArgumentNullException>(() => TrendMatrix.Build(null!));

        Assert.Equal("iterations", thrown.ParamName);
    }

    [Fact]
    public void Of_RejectsNullCandidates()
    {
        ArgumentNullException thrown = Assert.Throws<ArgumentNullException>(
            () => TrendIteration.Of(Approximation.Exact(Ratio(6, 1)), null!));

        Assert.Equal("candidates", thrown.ParamName);
    }

    [Fact]
    public void Of_CopiesTheCandidatesItIsGiven()
    {
        Approximation enclosure = Approximation.Exact(Ratio(6, 1));

        var fromList = new List<RationalCandidate> { RationalCandidate.Against(Ratio(6, 1), enclosure) };
        TrendIteration ofList = TrendIteration.Of(enclosure, fromList);
        fromList.Add(RationalCandidate.Against(Ratio(26, 1), enclosure));

        Assert.Single(ofList.Candidates);

        // An array is the case where skipping the copy is easiest and most tempting, so it is the
        // case worth pinning rather than the one a List already covers.
        var fromArray = new[] { RationalCandidate.Against(Ratio(6, 1), enclosure) };
        TrendIteration ofArray = TrendIteration.Of(enclosure, fromArray);
        fromArray[0] = RationalCandidate.Against(Ratio(26, 1), enclosure);

        Assert.Equal(Ratio(6, 1), ofArray.Candidates[0].Value);
    }

    [Fact]
    public void Build_ReadsOnlyTheValueAndHeightOfACandidate()
    {
        // Candidates judged against a different enclosure produce the same row, which is what
        // makes it safe to name a control once and let density do the rest.
        Approximation elsewhere = Approximation.Create(Ratio(1000, 1), Ratio(1, 1));

        TrendMatrix viaOwnEnclosure = TrendMatrix.Build([IterationAt(Ratio(5, 1), Ratio(6, 1))]);
        TrendMatrix viaForeignEnclosure = TrendMatrix.Build(
        [
            TrendIteration.Of(Approximation.Exact(Ratio(5, 1)), [RationalCandidate.Against(Ratio(6, 1), elsewhere)]),
        ]);

        Assert.Equal(viaOwnEnclosure.Rows[0].Candidate, viaForeignEnclosure.Rows[0].Candidate);
        Assert.Equal(viaOwnEnclosure.Rows[0].Height, viaForeignEnclosure.Rows[0].Height);
        Assert.Equal(viaOwnEnclosure.Rows[0].Distances, viaForeignEnclosure.Rows[0].Distances);
    }
}
