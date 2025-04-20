using System.Diagnostics.CodeAnalysis;

namespace Choir;

public enum TriState
{
    Unknown = 0,
    True = 1,
    False = -1,
}

public static class TriStateExtensions
{
    public static TriState Not(this TriState self) => self switch
    {
        TriState.False => TriState.True,
        TriState.True => TriState.False,
        TriState.Unknown or _ => TriState.Unknown,
    };

    public static TriState And(this TriState left, TriState right) => left switch
    {
        TriState.False => TriState.False,
        TriState.True => right,
        TriState.Unknown or _ => right == TriState.False ? TriState.False : TriState.Unknown,
    };

    public static TriState Or(this TriState left, TriState right) => left switch
    {
        TriState.False => right,
        TriState.True => TriState.True,
        TriState.Unknown or _ => right == TriState.True ? TriState.True : TriState.Unknown,
    };

    public static TriState Xor(this TriState left, TriState right) => left == TriState.Unknown || right == TriState.Unknown ? TriState.Unknown : (left == right ? TriState.False : TriState.True);
}

public readonly struct Trilean(TriState state = TriState.Unknown)
    : IEquatable<Trilean>
{
    public static readonly Trilean False = new(TriState.False);
    public static readonly Trilean Unknown = new(TriState.Unknown);
    public static readonly Trilean True = new(TriState.True);

    public static implicit operator Trilean(TriState state) => new(state);
    public static implicit operator Trilean(TriState? state) => new(state ?? TriState.Unknown);
    public static implicit operator TriState(Trilean trilean) => trilean.State;
    public static implicit operator Trilean(bool b) => new(b ? TriState.True : TriState.False);
    public static implicit operator Trilean(bool? b) => new(b is null ? TriState.Unknown : b.Value ? TriState.True : TriState.False);
    public static explicit operator bool(Trilean trilean) => trilean.State == TriState.True;
    public static explicit operator bool(Trilean? trilean) => trilean is { State: { } state } && state == TriState.True;
    public static explicit operator bool?(Trilean trilean) => trilean.State == TriState.Unknown ? null : trilean.State == TriState.True;

    public static Trilean operator ~(Trilean self) => new(self.State.Not());
    public static Trilean operator !(Trilean self) => new(self.State.Not());
    public static Trilean operator &(Trilean left, Trilean right) => new(left.State.And(right.State));
    public static Trilean operator |(Trilean left, Trilean right) => new(left.State.Or(right.State));
    public static Trilean operator ^(Trilean left, Trilean right) => new(left.State.Xor(right.State));
    
    public static bool operator ==(Trilean left, Trilean right) => left.Equals(right);
    public static bool operator ==(Trilean left, TriState right) => left.State == right;
    public static bool operator ==(TriState left, Trilean right) => left == right.State;
    public static bool operator !=(Trilean left, Trilean right) => !(left == right);
    public static bool operator !=(Trilean left, TriState right) => left.State != right;
    public static bool operator !=(TriState left, Trilean right) => left != right.State;

    public readonly TriState State = state;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Trilean other && Equals(other);
    public bool Equals(Trilean other) => State == other.State;
    public override int GetHashCode() => State.GetHashCode();
    public override string ToString() => State.ToString();
}

public static class TrileanExtensions
{
    public static Trilean Canonical(this Trilean self) => self;
    public static Trilean Canonical(this Trilean? self) => self ?? Trilean.Unknown;
}
