# RationalApproximation

A .NET 10 class library for **rational approximation carrying proven error
bounds**. Given a real quantity supplied as an exact enclosure — a value
together with a bound on how far it can be from the truth — this layer finds
the rational approximations to it and reports what the enclosure lets you
conclude about them.

Everything is exact rational arithmetic (`BigRational`). No decimals upstream
of presentation.

## What lives here, and what does not

**This layer holds no concrete constants.** There is no π here, no ζ, no √2.
It knows nothing about which real numbers are interesting. Concrete constants —
each provider proving its own truncation bound — live in `RealConstants`, one
layer up; the investigation that consumes them lives in `Zeta`, one layer above
that.

The separation is the point. This layer is a general instrument, testable
against any enclosure, and is not shaped by any single hunt.

**That bars implementations, not abstractions and combinators over them.**
`AffineConstant` — `offset + scale · inner`, for any inner constant — satisfies
the provider contract without naming a real, and `ConstantRun` drives one
without knowing which. The test is whether the type would have to change to
point the bench at a different constant.

Design: `SPEC-rational-ratio.md` in the
[umbrella repository](https://github.com/halheinrich/Math).

## Projects

- `RationalApproximation` — main library
- `RationalApproximation.Tests` — xUnit tests

## Building

**This repository does not build standalone.** It references
`BigRationalLibrary` by `ProjectReference`, and that reference escapes the
repo:

```
..\..\BigRationalLibrary\BigRationalLibrary\BigRationalLibrary.csproj
```

That resolves only when this checkout sits beside a `BigRationalLibrary`
checkout, as it does inside the umbrella:

```
Math/
  BigRationalLibrary/
  RationalApproximation/     <- here
```

A clone of this repository alone cannot restore. This is the accepted price of
the umbrella's `ProjectReference` ruling, not an oversight; the build-and-test
workflow reconstructs that layout rather than pretending otherwise.

```powershell
dotnet build
```

## Test

```powershell
dotnet test
```
