using System.Globalization;

namespace Terrace.MasterData.Builder.Discovery;

/// <summary>主キーの値(複合キー対応)。値ごとの等価性で比較する。</summary>
public sealed class PrimaryKeyValue : IEquatable<PrimaryKeyValue>
{
    private readonly object?[] _values;

    public PrimaryKeyValue(params object?[] values)
    {
        _values = values;
    }

    public IReadOnlyList<object?> Values => _values;

    public bool Equals(PrimaryKeyValue? other)
    {
        if (other is null || other._values.Length != _values.Length) return false;
        for (var i = 0; i < _values.Length; i++)
        {
            if (!object.Equals(_values[i], other._values[i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as PrimaryKeyValue);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _values) hash.Add(value);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        static string Str(object? v) => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return _values.Length == 1 ? Str(_values[0]) : "(" + string.Join(", ", _values.Select(Str)) + ")";
    }
}
