# RationalApproximation

> Collaboration contract → `../AGENTS.md`.
> Cross-cutting status & dependency graph → `../INSTRUCTIONS.md`.
> Mission, principles & repo conventions → `../VISION.md`.

The deep working reference for this submodule. Ratified design for the
investigation it serves → `../SPEC-rational-ratio.md`.

## Stack

A C# class library and its xUnit test project; language version, target
framework and namespace conventions are umbrella-wide and live in
`../VISION.md` and `Directory.Build.props`.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\Math\RationalApproximation\RationalApproximation.slnx`

## Repo

`https://github.com/halheinrich/RationalApproximation`, branch `main`.

## Depends on

- **BigRationalLibrary** — `HalHeinrich.Numerics.BigRational`, the exact
  rational this layer computes in. Used for its arithmetic and comparison
  operators and `CompareTo`; `Abs`, `Pow`,
  `Round(BigRational, MidpointRounding)`; `Zero` / `One` / `FromInteger` and the
  `(BigInteger, BigInteger)` constructor; `Sign` and `IsZero`; and its
  `Numerator` / `Denominator` parts — which are always the reduced form with a
  positive denominator, a fact `RationalCandidate.Height` depends on.

  By `ProjectReference`, per the umbrella's ruling on intra-umbrella edges, at
  `..\..\BigRationalLibrary\BigRationalLibrary\BigRationalLibrary.csproj`. That
  path **escapes this repository** — see § Pitfalls.

## Layout

- **`RationalApproximation`** — the library. The whole public surface, and no
  concrete constants (see § Architecture).
- **`RationalApproximation.Tests`** — xUnit. Also holds the reference oracles
  and the harness the library is checked against, which are part of the design
  rather than scaffolding: `BruteForce` (an independent implementation of what
  `DenominatorSweep` claims), `BoundedSearch` (the hang guard), `Sampling` and
  `TestConstants`. It sees `internal` members by `InternalsVisibleTo`.

## Architecture

**This layer holds no concrete constants.** No π, no ζ, no √2. It knows nothing
about which reals are interesting; providers live one layer up in
`RealConstants` and the investigation one layer above that. The separation is
what makes this a general instrument rather than one hunt's private machinery.

### `Approximation` — the enclosure everything is built on

A `readonly record struct` of a `Value` and a proven `MaxError`, with the
invariant `|true - Value| <= MaxError`. No public constructor: there is no
signature that could not be handed a negative radius, so construction goes
through `Exact` or `Create`. The default value is exact zero, which is a valid
state — an unknown known to be exactly zero.

Deliberately **not** `IComparable<T>`. Enclosures are only partially ordered and
two that overlap have no defined order; implementing it would let the compiler
accept a sort on something with no total order. The decidable predicates —
`Contains`, `ExcludesZero`, `IsExact` — are exposed instead. The same objection
recurs at `RationalCandidate` below, and is why "strictly better" is measured
against `Value`.

Three propagation facts are **load-bearing, and are not slack to be optimised
away**:

- **`Multiply`'s second-order term.** The bound is
  `|a|*beta + |b|*alpha + alpha*beta`. Dropping the final term gives the
  first-order bound, which is *unsound* — and a test using only narrow
  enclosures will not notice, because narrow is exactly where the term is
  negligible.
- **`Divide`'s bound is tight, not merely sound.** It is the tightest symmetric
  enclosure of the quotient about `a/b` this type can represent, so no accuracy
  is being given away and there is nothing to reclaim by rewriting it. Its
  validity condition `|b| > beta` is the same condition as `ExcludesZero`, and
  so the same condition as the throw — a divisor whose enclosure contains zero
  makes the quotient unbounded *even when its `Value` is non-zero*.
- **`Pow` is not repeated multiplication.** It computes the exact image of the
  input interval and re-centres on it, so its `Value` is generally *not* the
  operand's `Value` raised to the power: squaring `0 +/- 1` gives `1/2 +/- 1/2`.
  `a * a` treats its operands as independent unknowns and yields `[-1, 1]`,
  which contains negatives no square can take. That is interval arithmetic's
  **dependency problem**, and re-centring is why it does not bite here. Even
  exponents are non-monotonic — an interval straddling zero attains its minimum
  at zero, not at a signed endpoint.

**`Coarsen` is a base-two logarithm ceiling**, computed from `BigInteger` bit
lengths. It is neither a nearest-rounding nor an integer ceiling: the result is
two to a *possibly negative* power, since a useful `MaxError` is far below one.
Rounding here is **directed up, never to nearest** — a nearest-rounding can
round a bound down, which is a defect and not a different choice. An exact
enclosure is returned unchanged, and that is the one case where the result's
`MaxError` is not a power of two: there is no power of two to round zero up to,
and widening a proven exactness would discard evidence rather than unread
digits.

### The two rounding sites, in opposite directions

The design's easiest thing to get wrong, so it is stated once here and
commented at both sites.

| Site | Direction | Why that direction |
| --- | --- | --- |
| `Approximation.Coarsen` | ceiling, directed up | a bound may widen, never narrow |
| `DenominatorSweep.NumeratorRounding` | nearest, `AwayFromZero` | exhaustiveness holds only for the *closest* rational |

Reaching for the other one at either site is a defect, not a preference.

### `IRealConstant` — a bound you can plan a run against

`ErrorBoundAt(step)` and `Refinements()` are declared; `StepFor` and
`ApproximateTo` are **default interface members**. Nothing in the type system
enforces the obligations below, so they are contract: an implementation that
breaks one is defective even though it compiles.

`ErrorBoundAt` must be pure, non-increasing, tending to zero, and **computable
without doing the step's work**. That last obligation is what earns the member —
a provider that computed the refinement in order to report its bound would
satisfy the signature and defeat the purpose. `Refinements` must be lazy,
endless, strictly improving and incremental.

`StepFor` doubles to bracket and then bisects, so planning a run at step 1000
costs about twenty bound evaluations and **no refinements at all**. Non-increase
is what makes "is this step good enough" monotone in the step, which is what
bisection needs; over a flat run of steps it returns the run's first step rather
than an arbitrary member of it. The boundary is inclusive. `ApproximateTo` stops
on the refinement's *realised* `MaxError` rather than on `ErrorBoundAt`, so it
never does more work than the bound would require and sometimes does less.

Both defaults reject a non-positive target: a bound tends to zero without
reaching it, so a target of zero could never be met and the search would not
terminate — refusing beats hanging. A bound that never improves, and a
`Refinements()` that ends, each raise a distinct exception naming the obligation
broken, so a bad provider is reported rather than silently accommodated.

### `IRationalApproximator` / `RationalCandidate` / `DenominatorSweep`

The search contract: find the simplest rational an enclosure permits. Named for
what it produces, not for a conclusion — the target's irrationality is the open
question the bench exists to probe, so a name asserting it would beg the
question in every file that used it.

`RationalCandidate` stores **only** the candidate's value and the enclosure it
was judged against. `Height`, `MinDistance`, `MaxDistance` and `IsEnclosed` are
all derived, so they cannot fall out of step with each other or with the
enclosure; there is no constructor that could be handed a height belonging to a
different fraction. There are two distances because the distance to the truth is
itself only known to within the enclosure, and reporting one without the other
would state a distance the evidence does not support. `Height` is a property of
the **reduced** fraction, never of the sweep index that produced it: a sweep at
denominator 2 yielding `12/2` carries height 6.

**"Strictly better" is measured against `Approximation.Value`** — the ruling on
the design point the spec left open. Distances to an enclosure are intervals and
only partially ordered; the value is the exact quantity being approximated, and
ordering by distance to it is total. The choice is not observable in the output:
for any candidate outside the enclosure the three measures differ by exactly
`MaxError` and so induce the same order, and every comparison a search makes is
against a candidate outside the enclosure. The improvement filter and the
stopping rule cannot conflict either — being enclosed means being within
`MaxError` of the value, and every earlier candidate was further away than that
or the search would already have stopped.

**`DenominatorSweep` is the reference and is never optimised.** Its only product
is being an oracle nobody has to argue about, so it stays trivially auditable
and slow. A floating-point prefilter would work arithmetically and is a standard
technique, but it would put a numerically delicate threshold at exactly the
decision this type exists to get right, with nothing left to validate *it*
against. A fast searcher is a third implementation behind the same interface,
never a change here. Denominators run `1, 2, 3, ...` unbounded, because the
enclosure decides where the search stops and a second limit would let it end
quietly without an answer. It always terminates, including on an exact
enclosure: the value is itself a `BigRational`, so at worst the sweep reaches
that value's own denominator.

**Two findings the spec did not anticipate**, recorded here because they change
what the API means:

1. The terminating candidate is the least-**denominator** enclosed rational, not
   always the least-**height** one. At denominator 1 the sweep considers only
   the nearest integer, so an enclosure holding two integers can report the
   further-from-zero one; `[1, 2]` is the minimal case, and it is not a
   tie-rule artifact. Every observed failure enclosed at least two integers and
   none had `MaxError` below `1/2`, so the claim holds for every enclosure
   narrower than 1 — which is every enclosure this bench will produce.
2. The reduction worry cannot materialise. A candidate reducing to a smaller
   denominator would not be a strict improvement on what that denominator had
   already yielded, so it is never emitted; every yielded candidate is therefore
   already in lowest terms, with its denominator equal to its sweep index.

### `TrendMatrix` / `TrendIteration` / `TrendRow`

Candidates × iterations of exact `|a/b - x_k|`: rows are candidates, columns are
iterations, cells are exact `BigRational` distances.

**No verdict member, and it is enforced rather than asked for.** There is no
`IsConverged`, no `Answer`, no `HasPlateaued`. Each would be the spec's
explicitly rejected stopping rule wearing a property name, and the first caller
to find one would read it as a verdict. Measured runs have shown a candidate
holding steady for two consecutive iterations and then moving on, so any
"unchanged for k rounds" rule with k = 2 false-positives on cases that actually
happened. **A reflection test bars any member on these three types whose name
reads as a verdict, and any member returning `bool` at all** — a boolean here
would be that same thing under any name. Both halves of the guard were probed
with members they should reject, and both were caught. If a genuinely
descriptive boolean is ever wanted, that test is where the argument has to be
made, rather than somewhere it can be added quietly.

`Build` takes the whole finished run at once. An accumulator readable mid-run
would invite exactly the early exit on apparent stability the design rejects; a
snapshot of a finished run cannot. Rows are ordered by height and then by value
— an index, not a ranking — so a candidate lands in the same position whichever
iteration surfaced it and whether or not the run is later extended, which is
what lets two runs of a growing experiment be read side by side. `FirstSeenAt`
carries the discovery history a first-appearance ordering would otherwise
encode.

**Rows are dense.** A candidate first surfaced at iteration 5 still has cells
for 0 to 4, because the distance to an earlier ratio needs nothing but the
candidate and that ratio. That falls out of the arithmetic, and it means a
rational **no search produced** gets a full row from being named in a single
iteration — which is how a positive control is watched without inventing a
provider for it. Only `Value` and `Height` are read when a matrix is built, so
passing a candidate judged against some other enclosure does no harm.

Which candidates deserve rows is the **caller's** decision, and deliberately not
this layer's. The natural feed is each iteration's terminating candidate, but a
caller may pass every improvement a search yielded, or add controls it wants
watched. Encoding a policy here would be this layer deciding what is worth
looking at.

### Internal pattern: a mechanism is never its own oracle

`BruteForce` is built from the definitions and **never calls the sweep** — a
test that reuses the mechanism proves only that it agrees with itself. It
enumerates reduced rationals by increasing height, decides enclosure membership
with `Approximation.Contains`, and finds integers in an interval with a directed
ceiling and floor, never with the nearest rounding the sweep depends on. It
checks nearestness by comparing a candidate against both of its neighbours at
its own denominator rather than by rounding. `IRealConstant`'s bisection is
likewise checked against a linear scan built from the definition.

The same rule governs the test doubles. `IRealConstant`'s doubles are the
*input* to the defaulted members under test, not stand-ins for them. Testing
those members against a real provider would be worse: the expected step would
have to be recomputed from that provider's own convergence formula, so an error
in the formula would cancel against the same error in the expectation and the
test would pass.

## Public API

Namespace `HalHeinrich.Numerics`.

```csharp
public readonly record struct Approximation
{
    public BigRational Value { get; }
    public BigRational MaxError { get; }      // never negative
    public BigRational Lower { get; }         // Value - MaxError
    public BigRational Upper { get; }         // Value + MaxError
    public bool IsExact { get; }              // MaxError is zero
    public bool ExcludesZero { get; }         // |Value| > MaxError

    public static Approximation Exact(BigRational value);
    public static Approximation Create(BigRational value, BigRational maxError);

    public bool Contains(BigRational value);  // endpoints included

    public static Approximation Add(Approximation left, Approximation right);
    public static Approximation Subtract(Approximation left, Approximation right);
    public static Approximation Multiply(Approximation left, Approximation right);
    public static Approximation Divide(Approximation left, Approximation right);
    // operators +, -, *, / delegate to those four

    public Approximation Pow(int exponent);
    public Approximation Coarsen();
}
```

`Create` throws `ArgumentOutOfRangeException` on a negative `maxError`.
`Divide` throws `DivideByZeroException` when the divisor does not
`ExcludesZero`, and `Pow` throws the same for a negative exponent on an
enclosure that does not. `Pow(0)` is exactly one for every enclosure, including
one containing zero.

```csharp
public interface IRealConstant
{
    // pure, non-increasing, tending to zero, cheap
    BigRational ErrorBoundAt(int step);

    // lazy, endless, strictly improving, incremental
    IEnumerable<Approximation> Refinements();

    // default members - least n with ErrorBoundAt(n) <= target, and the first
    // refinement whose realised MaxError is at or below it
    int StepFor(BigRational targetError);
    Approximation ApproximateTo(BigRational targetError);
}
```

Both defaults throw `ArgumentOutOfRangeException` on a non-positive target, and
`InvalidOperationException` naming the obligation broken when the bound does not
converge or `Refinements()` ends.

```csharp
public interface IRationalApproximator
{
    IEnumerable<RationalCandidate> Search(Approximation enclosure);
}

public sealed class DenominatorSweep : IRationalApproximator
{
    public const MidpointRounding NumeratorRounding = MidpointRounding.AwayFromZero;

    public IEnumerable<RationalCandidate> Search(Approximation enclosure);
}

public readonly record struct RationalCandidate
{
    public BigRational Value { get; }
    public BigInteger Height { get; }         // of the REDUCED fraction
    public BigRational MinDistance { get; }   // zero when enclosed
    public BigRational MaxDistance { get; }
    public bool IsEnclosed { get; }

    public static RationalCandidate Against(
        BigRational value, Approximation enclosure);
}
```

`Search` is lazy; each candidate is strictly closer to `enclosure.Value` and of
strictly greater `Height` than its predecessor; the sequence ends with the first
candidate whose `IsEnclosed` is true, and yields nothing after it.

```csharp
public sealed class TrendMatrix
{
    public IReadOnlyList<Approximation> Ratios { get; }  // the columns
    public IReadOnlyList<TrendRow> Rows { get; }         // by height, then value

    public static TrendMatrix Build(IEnumerable<TrendIteration> iterations);
}

public sealed class TrendIteration
{
    public Approximation Ratio { get; }
    public IReadOnlyList<RationalCandidate> Candidates { get; }

    public static TrendIteration Of(
        Approximation ratio, IEnumerable<RationalCandidate> candidates);
}

public sealed class TrendRow            // constructed only by TrendMatrix.Build
{
    public BigRational Candidate { get; }
    public BigInteger Height { get; }
    public int FirstSeenAt { get; }
    public IReadOnlyList<BigRational> Distances { get; }  // dense, one per column
}
```

`Build` and `Of` throw `ArgumentNullException` on a null sequence, and `Of`
copies its input so a later change to the source has no effect. A run with no
iterations yields an empty matrix, which is honestly empty rather than an error.

## Pitfalls

- **This repository does not build standalone.** The `ProjectReference` to
  `BigRationalLibrary` escapes the repo and resolves only when this checkout
  sits beside a `BigRationalLibrary` checkout, as it does inside the umbrella. A
  clone of this repository alone cannot restore. That is the accepted price of
  the `ProjectReference` ruling, not an oversight; the build-and-test workflow
  reconstructs the sibling layout rather than pretending otherwise, and takes
  `BigRationalLibrary` at `main` rather than at the SHA the private umbrella
  pins — so this gate can go red for a reason upstream of this repo, and the fix
  is then upstream.
- **`StepFor` and `ApproximateTo` are invisible on a concrete provider's own
  type.** They are default interface members, reachable only through an
  `IRealConstant`-typed reference. The remedy is to hold the interface; an
  implementation that wants them on its own surface must re-declare them, and
  should then delegate rather than reimplement. This caught the tests first, and
  it will catch every provider written against this contract.
- **Do not conflate the two rounding sites.** See the table in § Architecture:
  coarsening is a directed ceiling, the sweep's numerator is a nearest rounding,
  and either one at the other's site is a defect.
- **Do not "simplify" the propagation bounds.** `Multiply`'s second-order term
  is required for soundness and narrow enclosures hide its absence; `Divide`'s
  bound is already tight and has nothing to reclaim.
- **Do not square by multiplying.** `a * a` is wider than `a.Pow(2)` and can
  admit values the true image excludes.
- **Do not optimise `DenominatorSweep`**, and do not put a bound on its
  denominator loop.
- **Do not add a `bool` to the trend types.** The reflection test will fail, and
  that test is the intended place for the argument.
- **An unbounded test of a search hangs rather than fails.** A defective search
  can yield improvements forever or spin without yielding at all, and either way
  an unbounded test reports nothing and burns the CI job's timeout — the same
  silence-reads-as-success mode the workflow's zero-test guard and its
  `timeout-minutes` exist to prevent. Use `BoundedSearch`, which caps the count
  deterministically and latches a time budget for the no-yield case.
- **xUnit v2 discovers only public test classes**, so CA1515 ("types can be made
  internal") is off for test files. Complying with it would not fail the build —
  it would discover nothing and report green, which is why the rule is
  suppressed rather than satisfied. It is kept as a standing guard even where
  the analyzer does not currently fire.
- **CI restores in locked mode.** `Directory.Build.props` gates
  `RestoreLockedMode` on `ContinuousIntegrationBuild`, which the workflow passes
  to every stage — so adding or bumping a package means committing the updated
  `packages.lock.json`, or CI fails at restore while local restore still works.

## Subproject-internal next steps

- **A logarithmic searcher behind `IRationalApproximator`**, validated against
  `DenominatorSweep` rather than replacing it. Entirely internal to this repo;
  unscheduled.
- **The trend types' shape was a proposal**, not an implementation of a ratified
  contract — the spec fixed the matrix's *content*, not its API. If that
  contract list is ever extended, this surface is what it reconciles against.

Cross-cutting obligations that need `RealConstants` — the spec's positive and
negative controls, and demonstrating on a real run the behaviour the trend
matrix exists to record — are tracked in `../INSTRUCTIONS.md` and are
deliberately not repeated here.
