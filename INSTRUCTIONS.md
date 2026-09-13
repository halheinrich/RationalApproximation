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

**That bars implementations, not abstractions and combinators over them.**
`AffineConstant` implements `IRealConstant` and belongs here; `ConstantRun`
drives one. Neither names a real. The admission test is whether the type would
have to be changed to point the bench at a different constant — a provider
would, a combinator parameterised by an arbitrary inner constant would not. The
rule reaches the tests too: their doubles converge to values chosen for the
shape of the arithmetic, never because the value is interesting.

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
never a change here. It always terminates, including on an exact enclosure: the
value is itself a `BigRational` `n/d`, so at worst the sweep reaches `d`.

Denominators nevertheless run `1, 2, 3, ...` unbounded — **not because no bound
could be imposed**, since `d` is one, but because such a bound is a *budget* and
never a correctness device. It buys a legible refusal in place of an
astronomically long but finite run, and how long a run is worth waiting for is
the caller's question rather than the type's. The gap is wide: against a target
of `7919/307` the sweep stops at denominator 39 while `d` is 307. This
rationale replaced one saying no bound *existed* and arguing
from that against ever imposing one — both halves wrong, and the second argued
against the very cap the test suite already runs under.

**Two findings the spec did not anticipate**, recorded here because they change
what the API means:

1. The terminating candidate is the least-**denominator** enclosed rational, not
   always the least-**height** one. At denominator 1 the sweep considers only
   the nearest integer, so an enclosure holding two integers can report the
   further-from-zero one; `[1, 2]` is the minimal case, and it is not a
   tie-rule artifact. Every observed failure enclosed at least two integers and
   none had `MaxError` below `1/2`, so the claim holds for every enclosure
   narrower than 1 — which is every enclosure this bench will produce.
   `HeightSweep` has no such exception; see below and § Pitfalls.
2. The reduction worry cannot materialise. A candidate reducing to a smaller
   denominator would not be a strict improvement on what that denominator had
   already yielded, so it is never emitted; every yielded candidate is therefore
   already in lowest terms, with its denominator equal to its sweep index.

### `HeightSweep` — the same claim, enumerated in height order

The second `IRationalApproximator`, and what it produces is the *ordering*, not
a different answer. Against a target of 6 the sweep proposes `6/1` at
denominator 1 and stops, so a step-by-step exhibit of that run shows one
candidate and no refutation; `HeightSweep` tries `1/1 2/1 3/1 4/1 5/1 6/1`,
which makes "are we trying other candidates" a visible answer rather than an
assertion. That exhibit is the whole reason the type exists.

**The axis is chosen from the target, not fixed:** numerators when
`|Value| > 1`, denominators otherwise. Naive height is `max(|p|, q)`, so above
one the numerator carries it and below one the denominator does, and a caller
wanting the least-height rational an enclosure admits should not have to know
which side of 1 its target sits on to pick a type. `SearchesNumerators` is
public because the axis decides what the terminal *means* — a numerator bound on
one side and a denominator bound on the other, and a bound reported without
saying which is not one a reader can use.

A *fixed* numerator axis would not merely be inconvenient below one; it would
stop being height order at all. Against a target near `0.34` the numerators
`1, 2, 3` pair with denominators `3, 6, 9`, so the heights visited jump by three
and all but the first of each family is discarded as unreduced. Choosing the
axis also dissolves this side's only hazard instead of guarding it: the
numerator axis divides by the target where the sweep multiplies by an integer,
and the division is reachable only where `|Value| > 1`, so a zero or near-zero
target never meets it.

**Below one this is not an independent implementation, and a cross-check there
proves nothing.** It delegates to `DenominatorSweep` rather than restating that
loop, so the non-independence is structural rather than a caveat someone has to
remember. Above one the two are genuinely different code, and that is the only
regime where their agreement is evidence.

**The closest rational of a given numerator is a bracket, not a rounding**, and
this is the easiest thing on this axis to get wrong. At a fixed *denominator*
the closest numerator is a nearest-rounding of `x*b`, because the numerator
enters `p/b − x` linearly. At a fixed *numerator* it is not: `a/b` is a
hyperbola in `b`, so the integer nearest `a/x` is not always the one minimising
`|a/b − x|`. At `x = 29/10` with `a = 10`, rounding gives 3 at distance `13/30`
while 4 is closer at `2/5`; the two disagree at 31 of the first 4000 numerators
of a target near 26. `BestDenominator` brackets `a/|x|` with a floor and its
successor and compares both exactly — so this site makes **no rounding
decision**, which is why it does not join the two in the table above.

**Its terminal is the least-height enclosed rational without qualification**,
which is where it differs from its sibling; § Pitfalls owns that divergence. It
costs a factor of the target's magnitude more, and that is not slack to reclaim:
where the enclosure contains at most one integer, both searches end at the
*same* rational, one having reached its denominator and the other its numerator,
so the ratio of indices examined **is** the target. With two or more integers
enclosed they can part. Measured on `7919/307`, inside the holding case: 129
against 5 at a radius of `1/100`, and 1006 against 39 at `1/10000`.

**The reduced-pair skip is an early-out, not a correctness device.** A
non-reduced pair equals one of smaller numerator whose own best denominator was
at least as close, so the improvement filter would drop it anyway — identical
output with and without it over 408 enclosures, and the mutant that removes it
reddens nothing. **That redundancy holds only because the denominator is chosen
by comparing distances**; under a rounded `a/x` the argument fails, so removing
the bracket and the skip together would look like one simplification and be two
defects.

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

### `SurvivorSearch` — refutation as a proof, not a trend

Given enclosures of one unknown and a denominator bound `Q`, the rationals `p/q`
in lowest terms with `q <= Q` that **every** enclosure contains.

A candidate outside an enclosure of the unknown is not the unknown — permanently,
whatever later enclosures do — so a refuted candidate never comes back and the
survivor set only ever shrinks. That is what makes it evidence rather than a
reading, and it is what a `TrendMatrix` row cannot offer. The proxy it replaces,
a row that persists while improving, was defeated three separate ways in the
exploration behind `halheinrich/Math#64`: by a near miss four orders of magnitude
closer than the answer, by a plateau that stopped being flat when the height cap
moved, and by a rival that was both unchanged and falling.

**Deliberately not an `IRationalApproximator`, and the name says so twice.** That
interface obliges an implementation to be lazy, strictly improving, of increasing
height, and terminating on enclosure; this contract keeps only the first. Many
enclosures rather than one, no ordering by distance or by height, and everything
still standing rather than the first hit. `Sweep` is likewise absent from the
name, because both types carrying it implement the interface this one does not.
Whether the contract deserves an interface of its own is a question for a second
implementation.

**The element type is `BigRational` and not `RationalCandidate`.** A candidate
binds a value to the *one* enclosure it was judged against, so `IsEnclosed`,
`MinDistance` and `MaxDistance` all read off that single enclosure. Returned from
a search over many it would carry a property that reads as "survives" and does
not mean it. A caller wanting distances holds the enclosures already.

**Canonical form, and what it actually costs here.** `BigRational` is always in
lowest terms, so no survivor can be a *mis-spelling* of another; what the walk
has to avoid is proposing one rational once per denominator that spells it. The
scratchpad that dropped the greatest-common-divisor step reported 1500 survivors
which were 1500 spellings of `6` — the pairs `(6q, q)` for `q` from 1 to 1500.
Unlike `HeightSweep`'s identical-looking skip this one is load-bearing: nothing
downstream filters, and removing it reddens thirteen tests.

**Seeded from the narrowest enclosure; decided against all of them.** One
enclosure is enough to refute, so seeding the walk with the narrowest does nearly
all the work for one enclosure's cost. The seed is a **cost** choice and not a
correctness one — any enclosure gives the same answer, because a candidate the
seed rejects is refuted by the seed — and the mutant that seeds from the *widest*
correspondingly reddens nothing. Intersecting is still strictly stronger than
filtering on any one of them, since enclosures do not nest: two of half-width
`1/10` centred on `6` and on `61/10` admit seven candidates of denominator at or
below 10 between them and share only two.

**The candidate space is never materialised**, which is why the reachable bound
is limited by time and not by memory: candidates are walked one at a time and
dropped at the first enclosure that excludes them. The *result* is lazy for the
same reason, and that buys a property an eager one could not — a caller may stop
at the first survivor. Argument validation is therefore eager while enumeration
is deferred, which a single iterator method cannot do: its body does not run
until the first `MoveNext`, so an argument fault would surface at some later
`foreach` with nothing left to say which call caused it.

**Ordering is promised: nondecreasing denominator, increasing value within one.**
It is what the walk produces anyway, so it costs nothing. It is **not** height
order, and the difference shows up inside a single denominator rather than across
them: `1 ± 4` holds nine integers, so the first yielded is `-3/1` at height 3
while the `0/1` it also holds has height 1. A caller wanting least height wants
`HeightSweep`.

**An empty enclosure set throws.** Nothing refutes, so every rational within the
bound survives — infinitely many, the numerator being unbounded — and an empty
result would report complete refutation from no evidence, the direction
`AGENTS.md` § Exactness discipline forbids in *report the bound, not the verdict*.
`Q = 0` yielding nothing is the other empty result and means "nothing was
examined". The two are indistinguishable in the value and are told apart only by
the bound reported beside it.

**The per-denominator integer range is a rounding site that does not join the
table**, and for a different reason than `HeightSweep.BestDenominator`. The
integers `p` with `lo <= p/q <= hi` are exactly those with `lo*q <= p <= hi*q`,
so the range is a ceiling and a floor of exact rationals — but the *direction* is
not what is load-bearing. A wider range is merely wasteful, since the extra
candidate is put to the same membership test and dropped by the seed. Measured:
replacing the ceiling with a nearest rounding reddens nothing, while a range one
short reddens thirteen tests at the lower end and fourteen at the upper. The
hazard is a **float** bound landing one short, not a wrong direction, and
exactness is governed already.

That safety comes from the seed being re-tested alongside every other enclosure,
so the exact range and the uniform membership test are **redundant with each
other**. Removing both would look like one simplification and be two defects —
the same pattern the reduced-pair skip above records, and the reason neither is
described as an optimisation.

**It implements `../SPEC-rational-ratio.md` § 2's two rational-target rules,
once** — `IsReachable`, and `IsIsolated` with `ExclusiveIsolationBound` the
number it decides against. The rules and their model are stated in the spec and
nowhere in this repo; the XML docs carry each member's contract and cite it.
They live here because a restated copy of the isolation rule was wrong three
times, each version agreeing with the others at denominator 1, the only case any
control had run (`halheinrich/Math#64`). Three decisions:

- **Two rules, two members, and no conjunction.** They fail in opposite
  directions — an unreachable target is refuted falsely, an unisolated one
  stands among rivals — and one yes-or-no would fold them back into the single
  rule the spec's § 1 once misread them as.
- **One definition of a valid bound for the type.** `ThrowIfInvalidBound` is
  the only place a bound is judged, and every member calls it. So
  `IsReachable(target, 0)` is `false` rather than refused — zero is a bound
  `Survivors` accepts, and nothing is what it returns there. Isolation below the
  target's denominator *is* refused: the survivor set there is empty rather
  than crowded, so neither yes nor no is true, and a no would report the crowded
  failure for the empty one.
- **The predicate takes a half-width**, not an `Approximation`, because both
  rules are wanted before a run, when no enclosure exists. A caller holding one
  passes its `MaxError` as held.

**The tests check each rule against `Survivors`, never against its own
formula**, and soundness at the worst-case centres — the target at either end —
since a centred enclosure is the best case and pins nothing. Above denominator 1
the bound is attained only at some `Q`, so the one test that sees a bound too
*small* finds those `Q` from the defining congruence rather than assuming one.
Measured by mutation, 2026-09-10: dropping the target's denominator reddens
three tests and no integer-target test, which is how the error survived in
prose; squaring it reddens that congruence test alone; `<=` for the predicate's
`<` reddens four, as does `>` for reachability's `>=`; deleting the reachability
refusal does not compile (`CA1823`), and narrowing it to refuse only a bound of
zero reddens the refusal test alone.

### `AffineConstant` — the one combinator

`offset + scale · inner`, for any inner `IRealConstant`. No series, no
truncation, no convergence argument of its own.

`Refinements()` is the composition and nothing else: `Approximation.Multiply`
and `Add` against *exact* operands. Multiplication's
`|a|*beta + |b|*alpha + alpha*beta` collapses to `|scale|*beta` when the scale's
radius is zero, so **the bound is exactly `|scale|` times the inner's and
nothing rounds** — an equality where the rest of this library states an
inequality. Written this way so the propagation rule stays at the one site it is
checked against, which is also why a negative scale costs nothing: the `|scale|`
is the `Abs` already inside `Multiply`, not a sign case written here.

**A zero scale is refused by the contract, not by defensiveness.** It would make
every refinement the exact value `offset`, identical to its predecessor, which
breaks strict improvement for *every* possible inner. With `|scale| > 0` the
inner's strict improvement carries through, so nothing was weakened to admit
this type — and that is what keeps an exact enclosure unreachable through any
conforming provider.

That is the answer to the route halheinrich/Math#58 fears, a runner handed a
constant that happens to be exact: no conforming constant is one. It does not
close the direct route. `Approximation.Exact` is public, so a caller can still
hand `DenominatorSweep.Search` a zero-width enclosure and wait for the value's
own denominator, and that call is what the issue's proposed guard would refuse.

`StepFor` is re-declared and delegates to `Inner.StepFor(target / |scale|)`,
exactly rather than approximately, since `|scale| > 0` makes the two conditions
equivalent under exact division. `ApproximateTo` is deliberately left on the
interface: delegating it would save nothing, and the asymmetry is the point —
`StepFor` plans a run at no refinements, `ApproximateTo` restarts the sequence
on every call.

### `ConstantRun` / `ConstantIteration` — one constant, no divisor

Enclose, sweep, iterate, assemble a `TrendMatrix`. The composition and nothing
else; every contract it wires is already this layer's.

**Not a degenerate ratio run, and it must not be built as one.** A ratio's
distinctive content is the error-share decision between two operands; with one
constant there is only one thing to advance, so the decision does not exist.
Reaching a one-operand question through a two-operand runner would manufacture a
divisor the problem does not have.

**One refinement sequence is held across the whole schedule and advanced**, so
the last iteration costs the depth it reaches rather than that depth times the
number of columns. `ApproximateTo` is never used: it is a fresh `foreach` over
`Refinements()`, so once per target would restart from step zero and make a run
cost the sum of its prefixes. `ConstantIteration.Step` is what makes that
observable — the last iteration's step is the whole run's refinement count.

Nothing coarsens here. The provider's realised `MaxError` is already proven and
exact, so widening it would only lose accuracy; the ratio pipeline coarsens
because a *propagated* bound grows, and that reason does not reach this far.

**What a run costs is not a function of its targets.** The two measured
regimes belong to `../SPEC-rational-ratio.md` § 2, "What a search costs", and
the type's own remarks point there rather than restate them. The consequence
for a caller is the part that belongs here: a schedule is never affordable
because a law says so, and the expensive regime is the near-miss shape this
bench exists to refuse.

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

There are now **three** searches over one claim — `DenominatorSweep`,
`HeightSweep`, and `BruteForce` — and the third is the arbiter of the other two.
It enumerates *every* reduced rational of each height rather than one per index,
so it cannot miss a candidate the other two ordered past, which is exactly the
failure neither of them could detect in the other.
`BruteForce.IsNearestOfItsNumerator` is the mirror of its denominator twin and
is written the same way, by comparing both neighbours rather than by rounding —
which is the whole point, since on that axis a rounding would be *wrong*.

### Internal pattern: lift an unobservable decision into a pure function

A decision reachable only through machinery that hides its effect cannot be
tested, and an untestable decision is an undefended one. The remedy is a seam:
split the decision out as a pure function and give the tests that, leaving the
machinery around it unchanged. `HeightSweep.BestDenominator` is `internal` for
exactly this reason — its tie rule has no observable effect on `Search`, so
nothing short of holding the method could defend it.

**Third sighting of the same answer in this project.** `CLAUDE.md` § Shell
records the first: three defects living behind a keypress were removed not by
more care but by splitting *reading* a line from *interpreting* it, making
`Interpret` a pure function with nineteen cases against it, so that only the
read stayed terminal-gated. Reach for this whenever a decision is unobservable
through the front door — the untestable surface should be one function wide.

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

public sealed class HeightSweep : IRationalApproximator
{
    // true exactly when |enclosure.Value| > 1, so the terminal's NUMERATOR is
    // the bound proved; false means its denominator is
    public static bool SearchesNumerators(Approximation enclosure);

    public IEnumerable<RationalCandidate> Search(Approximation enclosure);
}
```

`HeightSweep.Search` delegates to `DenominatorSweep` whenever
`SearchesNumerators` is false, so below one the two return the identical
sequence. `BestDenominator` is `internal`, deliberately and load-bearingly —
see § Pitfalls.

```csharp
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

```csharp
public static class SurvivorSearch
{
    // lazy; nondecreasing denominator, increasing value within one; each
    // survivor yielded exactly once
    public static IEnumerable<BigRational> Survivors(
        IEnumerable<Approximation> enclosures, BigInteger denominatorBound);

    // SPEC § 2's rational-target rules; the spec states them, these implement them
    public static bool IsReachable(
        BigRational target, BigInteger denominatorBound);
    public static BigRational ExclusiveIsolationBound(     // EXCLUSIVE: isolates
        BigRational target, BigInteger denominatorBound);  // only strictly below
    public static bool IsIsolated(
        BigRational target, BigInteger denominatorBound, BigRational halfWidth);
}
```

`Survivors` throws `ArgumentNullException` on a null sequence,
`ArgumentException` on an empty one, and `ArgumentOutOfRangeException` on a
negative bound — all **at the call**, not at the first step, which is why the
method is not itself an iterator. `enclosures` is read once and copied, so a
lazy sequence is a fine argument and a later change to the source has no effect.
A bound of zero yields nothing, a denominator being positive.

All four members refuse a negative bound with the same
`ArgumentOutOfRangeException`, from one check. `IsReachable` answers every other
bound, zero included. `ExclusiveIsolationBound` and `IsIsolated` also throw
`ArgumentOutOfRangeException` on `denominatorBound` when `IsReachable` is
false, and `IsIsolated` on a negative `halfWidth`; a zero half-width is an exact
enclosure and is permitted.

```csharp
public sealed class AffineConstant : IRealConstant
{
    public AffineConstant(
        BigRational offset, BigRational scale, IRealConstant inner);

    public BigRational Offset { get; }
    public BigRational Scale { get; }          // never zero
    public IRealConstant Inner { get; }

    public BigRational ErrorBoundAt(int step);            // |Scale| * inner's
    public IEnumerable<Approximation> Refinements();
    public int StepFor(BigRational targetError);          // delegates, exactly
}
```

The constructor throws `ArgumentNullException` on a null `inner` and
`ArgumentOutOfRangeException` on a zero `scale`. `ErrorBoundAt` rejects a
negative step; `StepFor` leaves a non-positive target to the inner's own check,
which the division cannot mask because dividing by a positive magnitude
preserves the sign. `ApproximateTo` is reachable only through the interface.

```csharp
public sealed class ConstantRun
{
    public IReadOnlyList<ConstantIteration> Iterations { get; }
    public TrendMatrix Matrix { get; }

    public static ConstantRun Execute(
        IRealConstant constant,
        IEnumerable<BigRational> targetErrors,
        IRationalApproximator? approximator = null);   // DenominatorSweep
}

public sealed class ConstantIteration   // constructed only by ConstantRun
{
    public BigRational TargetError { get; }
    public int Step { get; }
    public Approximation Enclosure { get; }
    public TrendIteration Trend { get; }
    public IReadOnlyList<RationalCandidate> Candidates { get; }
    public RationalCandidate Simplest { get; }
}
```

`Execute` throws `ArgumentNullException` on a null constant or schedule, and
`ArgumentException` when the targets are not strictly decreasing or the last is
not positive. An empty schedule is an honestly empty run and pulls no
refinements at all. `Simplest` throws `InvalidOperationException` when the
search yielded nothing, which only a defective approximator can do.

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
  `IRealConstant`-typed reference. An implementation that wants them on its own
  surface must re-declare them, and should then delegate rather than
  reimplement. This caught the tests first, and it will catch every provider
  written against this contract.

  **"Hold the interface" is the remedy only for a type that has *not*
  re-declared** — corrected 2026-09-06, when `AffineConstant` made the other
  case real. Once a type re-declares, holding the interface is no longer
  merely unnecessary: `CA1859` makes an interface-typed local an **error**
  under `AnalysisMode=All`, so the two halves of the advice apply to disjoint
  types and following the wrong half will not compile. For a re-declaring type
  hold the concrete type and cast at the one site that still needs a defaulted
  member. Fifteen locals moved that way in `AffineConstantTests`; the single
  `ApproximateTo` call site keeps its cast.
- **Do not conflate the two rounding sites.** See the table in § Architecture:
  coarsening is a directed ceiling, the sweep's numerator is a nearest rounding,
  and either one at the other's site is a defect.
- **Do not "simplify" the propagation bounds.** `Multiply`'s second-order term
  is required for soundness and narrow enclosures hide its absence; `Divide`'s
  bound is already tight and has nothing to reclaim.
- **Do not square by multiplying.** `a * a` is wider than `a.Pow(2)` and can
  admit values the true image excludes.
- **Do not optimise `DenominatorSweep`**, and do not put a bound on its
  denominator loop. The same holds for `HeightSweep`, which is slower still by a
  factor of the target's magnitude, on purpose. Neither loop is unbounded for
  want of a bound — see § Architecture; a cap is a caller's budget and neither
  type takes one.
- **The two searches' terminals are not interchangeable, and they diverge
  exactly where a reader is most likely to be watching.** `DenominatorSweep`
  stops at the least-**denominator** enclosed rational, `HeightSweep` at the
  least-**height** one, and the two differ only when the enclosure holds two or
  more integers — measured over 5387 enclosures above one, that is the *only*
  disagreement. `[1, 2]` yields `2/1` from the sweep and `1/1` from height order;
  `12 ± 1` yields `12` and `11`. Two integers permit the divergence and do not
  force it: `6/5 ± 9/10`, which is `[3/10, 21/10]`, holds 1 and 2, and both
  searches end at `1/1`, because the integer nearest `6/5` is also the one of
  least height. A wide enclosure is what an early iteration
  looks like, so a caller that treats the two as substitutes is wrong at the
  start of a run and right later, which is the worst order to be wrong in.
  `HeightSweep`'s version of the claim is unconditional: above one, an enclosure
  reaching down to 1 must contain 1, so height 1 is found first, and one that
  does not reach it contains only rationals with `|p| > q` — exactly the family
  enumerated.
- **`HeightSweep.BestDenominator` is `internal` on purpose, and the visibility
  is load-bearing.** Its tie rule — on a tie take the smaller denominator, the
  candidate further from zero — has **no observable effect through `Search`**. A
  tie at numerator `a` between denominators `b` and `b+1` puts both candidates
  at distance `a/(2b(b+1))`, and a numerator whose ideal denominator falls
  exactly halfway between two integers fits the target so badly that neither of
  its candidates is ever a strict improvement; measured over 34111 tie
  configurations, flipping the rule changes no yielded sequence. So the only
  test that can defend the rule is one holding the method directly. Tidy it back
  to `private` as unnecessary surface and that test goes with it, the rule
  becomes undefended, and **nothing goes red**. Deleting the rule instead is also
  wrong: an oracle must be deterministic whether or not you can watch it choose.
- **Do not decide a survivor on one enclosure — and do not trust a
  two-enclosure fixture to catch it if you do.** `SurvivorSearch` seeds its walk
  from the *narrowest* enclosure, so with only two of them a mutant deciding
  membership on "the last" has already been filtered by the seed and returns the
  right answer anyway. Fixture C, the non-nesting pair this type's brief singled
  out for exactly this hazard, passed such a mutant; so did every other fixture
  in the class, and so did the reversed-order check written precisely to vary
  the seed. Catching it needs **three** enclosures with the sole refuter in the
  middle, which is what
  `Survivors_CanBeRefutedByAnEnclosureThatIsNeitherTheSeedNorTheLast` is.
  Measured 2026-09-08 by that type's own mutation run, and the general shape is
  worth carrying: a fixture chosen to exhibit a property can still be too small
  to distinguish the mechanism from a cheaper wrong one beside it.
- **`SurvivorSearch` must not be made an `IRationalApproximator`,** nor renamed
  to carry `Sweep`. It differs in every obligation that interface imposes but
  laziness — see § Architecture — and declaring the kinship, in the type list or
  in the name, would make the compiler accept callers that cannot be correct.
- **An empty survivor set means two different things,** and only the bound
  reported beside it tells them apart. Against the exact enclosure on `1/7` a
  bound of 6 yields nothing because the one rational that enclosure holds has a
  denominator the bound never reaches — raise it to 7 and `1/7` appears. Against
  two disjoint enclosures a bound of 60 yields nothing because nothing can
  satisfy both, and no bound will change that. The first is a statement about
  the budget, the second about the unknown, and the value is identical.
- **Do not refine *to* `ExclusiveIsolationBound`.** It is exclusive, while this
  library's refinement contracts are inclusive — `StepFor`, `ApproximateTo`
  and `ConstantRun` all stop at or below a target — so refining to it may return
  an enclosure of exactly that half-width, and one of those with the target at
  an end can reach a rival. `Coarsen` compounds it: at an integer target under a
  power-of-two `Q` the bound is itself a power of two, so an enclosure strictly
  inside it can be coarsened onto it. A strict bound has no inclusive equivalent
  over the rationals, so no value handed to an at-or-below refiner is "exactly
  isolating", and the name carries the exclusivity to every call site for that
  reason. Refine, ask `IsIsolated` of the half-width actually held after any
  coarsening, and refine again while it says no. **What coarsening loses is the
  proof, not the result** — measured 2026-09-10, an enclosure coarsened onto the
  bound still leaves the target alone, because coarsening keeps a centre the
  narrower radius placed; but the half-width held no longer establishes it, and
  a claim here is only as good as its proof.
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
- **Restore runs in locked mode everywhere, local as well as CI.**
  `Directory.Build.props` sets `RestoreLockedMode` unconditionally, so adding or
  bumping a package fails restore instead of quietly updating
  `packages.lock.json`. Recover with
  `dotnet restore -p:RestoreForceEvaluate=true` and commit the regenerated lock;
  `dotnet add package` will fail until you do. It was gated on
  `ContinuousIntegrationBuild` until #29, which the workflow still passes to
  every stage. What locked mode does *not* cover — a version arriving through a
  `ProjectReference` rather than a `PackageReference` — is recorded in
  `Directory.Build.props`.
- **A near-miss row is indistinguishable from a find at every reachable
  precision.** This is a property of the method, not of one fixture, and it is
  the sharpest reason no `IsConverged` may ever exist on these types. For a
  constant sitting `δ` above a simple rational `r`, the row for `r` has cells
  `|ε_k − δ|`, so while `ε_k ≫ δ` the row **falls at every column**, exactly as
  a genuine find does. It floors at `δ` and no schedule that does not pass `δ`
  can see the floor. Measured 2026-09-06 with `δ = 10⁻³⁰`: the row fell at every
  one of five columns and nothing in the matrix separated it from an answer.
  What a run earns is a bound on **the axis its searcher enumerates** — a
  denominator bound under `DenominatorSweep`, a height bound under `HeightSweep`
  above one — together with the survivor set under a bound fixed in advance. A
  falling row is not a second, weaker verdict to be read alongside either.
- **The scaling rule is stated twice in `AffineConstant`, deliberately, and must
  not be single-sourced.** `Refinements()` gets `|scale|` implicitly, from the
  `Abs` inside `Approximation.Multiply`; `ErrorBoundAt` states it explicitly as
  `magnitude * Inner.ErrorBoundAt(step)`. They cannot be unified, because
  `ErrorBoundAt` is obliged to be computable *without* doing the step's work and
  so cannot go through a refinement. What keeps them honest is the test
  asserting that `ErrorBoundAt` upper-bounds the realised `MaxError` of the same
  step — the bridge between the two routes. Deleting the duplication means
  deleting the bridge. Evidence that it works: dropping the `Abs` reddens tests
  on both sides at once.
- **An expectation about which rationals an enclosure admits must be checked
  against the provider's enclosure shape.** `HalvingConstant` is one-sided —
  `Value + MaxError == Truth` at every step — and that is incidental to it, not
  required by any contract. A rational an arbitrarily small distance *above* the
  truth is outside every such enclosure and one the same distance below is
  inside. Measured 2026-09-06: one limit, one schedule, terminating at
  denominator 3 under a centred enclosure and at 43693 under this one. A control
  whose property survives only one of the two shapes is not testing what its
  name says.
- **`AnalysisMode=All` has seven times been the earlier witness this arc**, which
  is the answer to anyone pricing its friction. `CA1859` turned the stale "hold
  the interface" remedy above from prose into a build error. And six separate
  mutants were refused by the compiler or an analyzer before any test could
  observe them: `CA1823` on `ConstantRun`, because removing the held enumerator
  orphaned its `RefinementsEndedMessage`; `CS0219` on `HeightSweep`, because
  removing the improvement filter orphaned its own bookkeeping; `CA1823` again
  on `HeightSweep`, because forcing the numerator axis made the delegated
  `DenominatorSweep` unreachable; `CA1823` a third time on `SurvivorSearch`,
  because replacing the empty-enclosure throw with an empty result orphaned its
  `NoEnclosuresMessage`; `CS0162` on the same type, because disabling the
  reduced-pair skip through a constant-false condition left its `continue`
  unreachable; and `CA1823` a fourth time, on `SurvivorSearch` again, because
  removing `ExclusiveIsolationBound`'s unreachable-target refusal orphaned its
  `UnreachableIsolationMessage`. **"Did not compile" is therefore a legitimate
  mutation-run outcome and not a failed experiment** — it is the same finding a
  red test would have been, arriving earlier, and it is the concrete evidence for
  this setting that the rest of these docs assert without showing. None was a
  style complaint; each was the first thing to notice a real change.

## Subproject-internal next steps

- **A logarithmic searcher behind `IRationalApproximator`**, validated against
  `DenominatorSweep` rather than replacing it. Entirely internal to this repo;
  unscheduled.
- **The trend types' shape was a proposal**, not an implementation of a ratified
  contract — the spec fixed the matrix's *content*, not its API. If that
  contract list is ever extended, this surface is what it reconciles against.
- **A validated `TargetSchedule`, to single-source the schedule rule.** Ruled
  2026-09-07; **not implemented, so the duplication stands.** The rule that a
  run's target errors must be strictly decreasing with a positive last element
  is written twice — `ConstantRun.FaultInTargets` here and `RatioRun`'s copy in
  `Zeta` — and by `AGENTS.md` § Writing code that is one rule in two places, so
  a correction can land on one and leave the other to contradict it. `Zeta`
  cannot be referenced from this layer, so the only direction that helps is
  exposing it from here. **Exposing the validator is the wrong fix**: it
  publishes a policy on the most-depended-on layer while leaving both callers
  free to skip it. The right one is a validated value type constructed through a
  factory, the shape `Approximation` already uses so that a negative radius is
  unrepresentable — which changes `ConstantRun.Execute`'s signature and requires
  `Zeta` to change with it. That is a two-repo change that has not been planned
  — no issue tracks it — and not a cheap one, and it is why the duplication was
  left standing rather than papered over.
  **The name is already taken.** `Zeta` ships a `static class TargetSchedule`
  (`../Zeta/Zeta/TargetSchedule.cs`), a generator of power-of-ten schedules for
  `RatioRun` — a different thing, whose own remarks discuss this very proposal.
  Both repos use the `HalHeinrich.Numerics` namespace and `Zeta` references this
  layer, so a validated type of that name here would share a fully qualified
  name with the generator inside `Zeta`'s compilation. Whoever builds it
  chooses another name or reconciles the two deliberately; neither is decided.

Cross-cutting obligations that need `RealConstants` — the spec's positive and
negative controls, and demonstrating on a real run the behaviour the trend
matrix exists to record — are tracked in `../INSTRUCTIONS.md` and are
deliberately not repeated here.
