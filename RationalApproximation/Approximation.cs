using System.Numerics;

namespace HalHeinrich.Numerics;

/// <summary>
/// An enclosure of an unknown real number: a <see cref="Value"/> together with a proven
/// upper bound <see cref="MaxError"/> on that value's distance from the truth.
/// </summary>
/// <remarks>
/// <para>
/// The invariant is <c>|true - Value| &lt;= MaxError</c>, equivalently that the unknown lies in
/// the closed interval <c>[Lower, Upper]</c>. Every operation on an <see cref="Approximation"/>
/// produces a bound valid for its result. Bounds widen; they never narrow silently.
/// </para>
/// <para>
/// Both components are <see cref="BigRational"/>, so every operation here is exact rational
/// arithmetic. No floating point is used anywhere in this type, including in comparisons.
/// </para>
/// <para>
/// The default value is exact zero, which is a valid state: an unknown known to be exactly zero.
/// There is no public constructor, because there is no way to offer one that cannot build a
/// negative radius; use <see cref="Exact"/> or <see cref="Create"/>.
/// </para>
/// <para>
/// This type is deliberately <b>not</b> <see cref="IComparable{T}"/>. Enclosures are only
/// partially ordered, and two that overlap have no defined order; implementing the interface
/// would let the compiler accept a sort on something with no total order. The decidable
/// predicates - <see cref="ExcludesZero"/>, <see cref="Contains"/>, <see cref="IsExact"/> - are
/// exposed instead.
/// </para>
/// </remarks>
public readonly record struct Approximation
{
    private const string DivisorEnclosesZeroMessage =
        "The divisor's enclosure contains zero, so the quotient is unbounded. This holds even " +
        "when the divisor's Value is non-zero: such a divisor has not been computed accurately " +
        "enough to divide by, and the caller must refine it first.";

    private const string ReciprocalEnclosesZeroMessage =
        "A negative exponent is a reciprocal, and this enclosure contains zero, so the result is " +
        "unbounded. This holds even when Value is non-zero; refine the enclosure first.";

    private Approximation(BigRational value, BigRational maxError)
    {
        Value = value;
        MaxError = maxError;
    }

    /// <summary>Gets the approximate value: the centre of the enclosure.</summary>
    /// <remarks>
    /// This is not necessarily the result of applying an operation to the operands' values. See
    /// <see cref="Pow"/>, which re-centres.
    /// </remarks>
    public BigRational Value { get; }

    /// <summary>
    /// Gets the proven upper bound on the distance from <see cref="Value"/> to the unknown truth.
    /// Never negative.
    /// </summary>
    public BigRational MaxError { get; }

    /// <summary>Gets the least value the unknown can take: <c>Value - MaxError</c>.</summary>
    public BigRational Lower => Value - MaxError;

    /// <summary>Gets the greatest value the unknown can take: <c>Value + MaxError</c>.</summary>
    public BigRational Upper => Value + MaxError;

    /// <summary>
    /// Gets a value indicating whether the enclosure has zero width, so <see cref="Value"/> is the
    /// unknown exactly.
    /// </summary>
    public bool IsExact => MaxError.IsZero;

    /// <summary>
    /// Gets a value indicating whether zero lies strictly outside the enclosure, equivalently
    /// whether <c>|Value| &gt; MaxError</c>.
    /// </summary>
    /// <remarks>
    /// This is precisely the condition under which this enclosure may be divided by, and the
    /// condition under which <see cref="Pow"/> accepts a negative exponent.
    /// </remarks>
    public bool ExcludesZero => BigRational.Abs(Value) > MaxError;

    /// <summary>Creates an exact enclosure: zero width, so the value is the unknown itself.</summary>
    /// <param name="value">The exact value.</param>
    /// <returns>An enclosure with <see cref="MaxError"/> zero.</returns>
    public static Approximation Exact(BigRational value) => new(value, BigRational.Zero);

    /// <summary>Creates an enclosure from a value and a proven bound on its error.</summary>
    /// <param name="value">The approximate value.</param>
    /// <param name="maxError">
    /// A proven upper bound on the distance from <paramref name="value"/> to the truth. Must be
    /// non-negative.
    /// </param>
    /// <returns>The enclosure.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxError"/> is negative.</exception>
    public static Approximation Create(BigRational value, BigRational maxError)
    {
        if (maxError.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxError),
                "MaxError is the radius of an enclosure and cannot be negative.");
        }

        return new Approximation(value, maxError);
    }

    /// <summary>Determines whether the given value lies within the enclosure, endpoints included.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> if <c>Lower &lt;= value &lt;= Upper</c>.</returns>
    public bool Contains(BigRational value) => Lower <= value && value <= Upper;

    /// <summary>Adds two enclosures. The error bounds add.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>An enclosure of the sum.</returns>
    public static Approximation Add(Approximation left, Approximation right) =>
        new(left.Value + right.Value, left.MaxError + right.MaxError);

    /// <summary>
    /// Subtracts one enclosure from another. The error bounds <b>add</b>; subtracting them would
    /// be unsound, because the two unknowns can err in opposite directions.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>An enclosure of the difference.</returns>
    public static Approximation Subtract(Approximation left, Approximation right) =>
        new(left.Value - right.Value, left.MaxError + right.MaxError);

    /// <summary>Multiplies two enclosures.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>An enclosure of the product.</returns>
    /// <remarks>
    /// The bound is <c>|a|*beta + |b|*alpha + alpha*beta</c>. The final second-order term is not
    /// optional: the first-order bound <c>|a|*beta + |b|*alpha</c> is <b>unsound</b>, and a test
    /// using only narrow enclosures will not notice.
    /// </remarks>
    public static Approximation Multiply(Approximation left, Approximation right)
    {
        BigRational a = left.Value;
        BigRational alpha = left.MaxError;
        BigRational b = right.Value;
        BigRational beta = right.MaxError;

        BigRational error = (BigRational.Abs(a) * beta) + (BigRational.Abs(b) * alpha) + (alpha * beta);
        return new Approximation(a * b, error);
    }

    /// <summary>Divides one enclosure by another.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>An enclosure of the quotient.</returns>
    /// <exception cref="DivideByZeroException">
    /// The divisor's enclosure contains zero, even if its <see cref="Value"/> is non-zero.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The bound is the specified propagation
    /// <c>(|b|*alpha + |a|*beta) / ((|b| - beta)*|b|)</c>, valid exactly when
    /// <c>|b| &gt; beta</c> - which is the same condition as <see cref="ExcludesZero"/>, and so
    /// the same condition as the throw.
    /// </para>
    /// <para>
    /// That bound is not merely sound, it is tight: it is exactly the tightest symmetric
    /// enclosure of the quotient about <c>a/b</c>. The numerator is the largest
    /// <c>|x*b - y*a|</c> over the box and the denominator the smallest <c>|y|*|b|</c>, and one
    /// corner attains both at once - taking y to whichever endpoint is nearer zero fixes the sign
    /// of the <c>-a*(y - b)</c> term, and x is then free to align with it. So no accuracy is
    /// given away here relative to what this type can represent.
    /// </para>
    /// </remarks>
    public static Approximation Divide(Approximation left, Approximation right)
    {
        if (!right.ExcludesZero)
        {
            throw new DivideByZeroException(DivisorEnclosesZeroMessage);
        }

        BigRational a = left.Value;
        BigRational alpha = left.MaxError;
        BigRational b = right.Value;
        BigRational beta = right.MaxError;

        BigRational absB = BigRational.Abs(b);
        BigRational error = ((absB * alpha) + (BigRational.Abs(a) * beta)) / ((absB - beta) * absB);
        return new Approximation(a / b, error);
    }

    /// <summary>Adds two enclosures.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>An enclosure of the sum.</returns>
    public static Approximation operator +(Approximation left, Approximation right) => Add(left, right);

    /// <summary>Subtracts one enclosure from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>An enclosure of the difference.</returns>
    public static Approximation operator -(Approximation left, Approximation right) => Subtract(left, right);

    /// <summary>Multiplies two enclosures.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>An enclosure of the product.</returns>
    public static Approximation operator *(Approximation left, Approximation right) => Multiply(left, right);

    /// <summary>Divides one enclosure by another.</summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <returns>An enclosure of the quotient.</returns>
    /// <exception cref="DivideByZeroException">
    /// The divisor's enclosure contains zero, even if its <see cref="Value"/> is non-zero.
    /// </exception>
    public static Approximation operator /(Approximation left, Approximation right) => Divide(left, right);

    /// <summary>Raises this enclosure to an integer power.</summary>
    /// <param name="exponent">The exponent.</param>
    /// <returns>An enclosure of the power.</returns>
    /// <exception cref="DivideByZeroException">
    /// <paramref name="exponent"/> is negative and this enclosure contains zero, even if
    /// <see cref="Value"/> is non-zero. A negative exponent is a reciprocal and obeys the same
    /// rule as <see cref="Divide"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This does not propagate a bound: it computes the <b>exact image</b> of the input interval
    /// under the map to the given power and re-centres on it. Because <see cref="BigRational"/> is
    /// exact, the re-centred midpoint and half-width are exact, so the result is the tightest
    /// enclosure of that image this type can represent.
    /// </para>
    /// <para>
    /// Re-centring means the result's <see cref="Value"/> is generally <b>not</b> the operand's
    /// <see cref="Value"/> raised to the power. Squaring <c>0 +/- 1</c> gives the image
    /// <c>[0, 1]</c>, so the result is <c>1/2 +/- 1/2</c>.
    /// </para>
    /// <para>
    /// Even exponents are non-monotonic. An interval straddling zero attains its minimum at zero,
    /// not at a signed endpoint.
    /// </para>
    /// <para>
    /// An exponent of zero yields exactly one for every enclosure, including one containing zero,
    /// following the convention of <see cref="BigRational.Pow"/>.
    /// </para>
    /// <para>
    /// Prefer this to multiplying an enclosure by itself. <c>a * a</c> treats its two operands as
    /// independent unknowns and is correspondingly wider - for <c>0 +/- 1</c> it yields
    /// <c>[-1, 1]</c>, which contains negatives no square can take. That is interval arithmetic's
    /// dependency problem, and this method is why it does not bite here.
    /// </para>
    /// </remarks>
    public Approximation Pow(int exponent)
    {
        if (exponent == 0)
        {
            return Exact(BigRational.One);
        }

        if (exponent < 0 && !ExcludesZero)
        {
            throw new DivideByZeroException(ReciprocalEnclosesZeroMessage);
        }

        // Off an interval containing zero, raising to the power n has derivative n*t^(n-1) of
        // constant sign, so the map is monotonic and its image is spanned by the endpoints. The
        // one exception is a positive even exponent over an interval that does contain zero.
        BigRational atLower = BigRational.Pow(Lower, exponent);
        BigRational atUpper = BigRational.Pow(Upper, exponent);

        BigRational low = atLower <= atUpper ? atLower : atUpper;
        BigRational high = atLower <= atUpper ? atUpper : atLower;

        if (exponent > 0 && int.IsEvenInteger(exponent) && Contains(BigRational.Zero))
        {
            low = BigRational.Zero;
        }

        BigRational two = BigRational.FromInteger(2);
        return new Approximation((low + high) / two, (high - low) / two);
    }

    /// <summary>
    /// Returns this enclosure with its <see cref="MaxError"/> rounded up to the next power of two.
    /// </summary>
    /// <returns>
    /// An enclosure with the same <see cref="Value"/> and a <see cref="MaxError"/> that is the
    /// least power of two greater than or equal to the current one - or this enclosure unchanged
    /// if it is already exact.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Always sound: the bound only ever widens. It discards digits nobody reads and stops bounds
    /// accumulating height as fast as the values they accompany do.
    /// </para>
    /// <para>
    /// This is a base-two logarithm ceiling, computed from <see cref="BigInteger"/> bit lengths.
    /// It is neither a nearest-rounding nor an integer ceiling: the result is two raised to a
    /// <b>possibly negative</b> power, since a useful <see cref="MaxError"/> is far below one.
    /// Rounding here is directed up, never to nearest - a nearest-rounding can round a bound down,
    /// which is a defect rather than a different choice.
    /// </para>
    /// <para>
    /// An exact enclosure is returned unchanged. There is no power of two to round zero up to, and
    /// widening an exactly-zero bound to a positive one would discard a proven exactness rather
    /// than unread digits. This is the one case in which the result's <see cref="MaxError"/> is
    /// not a power of two.
    /// </para>
    /// </remarks>
    public Approximation Coarsen()
    {
        if (IsExact)
        {
            return this;
        }

        // MaxError is positive and reduced, so both parts are positive.
        BigInteger p = MaxError.Numerator;
        BigInteger q = MaxError.Denominator;

        // With bp and bq the bit lengths, 2^(bp-1) <= p < 2^bp and 2^(bq-1) <= q < 2^bq, so p/q
        // lies in [2^(bp-bq-1), 2^(bp-bq+1)). The least k with 2^k >= p/q is therefore no smaller
        // than bp-bq-1; starting there and stepping up terminates within two steps and cannot
        // overshoot, which is what makes the result the *next* power of two rather than merely an
        // upper one.
        int k = checked((int)(p.GetBitLength() - q.GetBitLength())) - 1;
        while (!IsPowerOfTwoAtLeast(k, p, q))
        {
            k++;
        }

        return new Approximation(Value, PowerOfTwo(k));
    }

    /// <summary>Tests whether two raised to k is at least p/q, for positive p and q, exactly.</summary>
    private static bool IsPowerOfTwoAtLeast(int k, BigInteger p, BigInteger q) =>
        k >= 0 ? (q << k) >= p : q >= (p << -k);

    /// <summary>Builds two raised to k as a <see cref="BigRational"/>, for k of either sign.</summary>
    private static BigRational PowerOfTwo(int k) =>
        k >= 0
            ? BigRational.FromInteger(BigInteger.One << k)
            : new BigRational(BigInteger.One, BigInteger.One << -k);
}
