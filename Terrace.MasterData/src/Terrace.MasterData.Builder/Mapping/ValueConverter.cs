using System.Globalization;

namespace Terrace.MasterData.Builder.Mapping;

/// <summary>
/// CSV のセル文字列をプロパティの型へ変換する。
/// - 空セルはその型の default(string は ""、配列は空配列、Nullable は null)
/// - bool は true/false(大文字小文字を問わない)/1/0
/// - 配列は `|` 区切り
/// - enum は名前(大文字小文字を問わない)または数値。定義にない数値はエラー
/// - 数値は InvariantCulture
/// </summary>
public static class ValueConverter
{
    public const char ArraySeparator = '|';

    public static bool TryConvert(string cell, Type targetType, out object? value, out string? error)
    {
        error = null;

        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying is not null)
        {
            if (cell.Trim().Length == 0)
            {
                value = null;
                return true;
            }
            return TryConvert(cell, underlying, out value, out error);
        }

        if (targetType == typeof(string))
        {
            value = cell;
            return true;
        }

        if (targetType.IsArray)
        {
            return TryConvertArray(cell, targetType.GetElementType()!, out value, out error);
        }

        var text = cell.Trim();
        if (text.Length == 0)
        {
            value = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            return true;
        }

        if (targetType.IsEnum)
        {
            return TryConvertEnum(text, targetType, out value, out error);
        }

        if (targetType == typeof(bool))
        {
            switch (text.ToLowerInvariant())
            {
                case "true":
                case "1":
                    value = true;
                    return true;
                case "false":
                case "0":
                    value = false;
                    return true;
                default:
                    value = null;
                    error = $"bool として解釈できません。true/false/1/0 を指定してください (value = {text})";
                    return false;
            }
        }

        var culture = CultureInfo.InvariantCulture;
        const NumberStyles IntegerStyle = NumberStyles.Integer;
        const NumberStyles FloatStyle = NumberStyles.Float | NumberStyles.AllowThousands;

        var ok = Type.GetTypeCode(targetType) switch
        {
            TypeCode.Int32 => Parse(int.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.Int64 => Parse(long.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.Int16 => Parse(short.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.Byte => Parse(byte.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.SByte => Parse(sbyte.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.UInt32 => Parse(uint.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.UInt64 => Parse(ulong.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.UInt16 => Parse(ushort.TryParse(text, IntegerStyle, culture, out var v), v, out value),
            TypeCode.Single => Parse(float.TryParse(text, FloatStyle, culture, out var v), v, out value),
            TypeCode.Double => Parse(double.TryParse(text, FloatStyle, culture, out var v), v, out value),
            TypeCode.Decimal => Parse(decimal.TryParse(text, FloatStyle, culture, out var v), v, out value),
            TypeCode.Char => Parse(text.Length == 1, text.Length == 1 ? text[0] : '\0', out value),
            TypeCode.DateTime => Parse(DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var v), v, out value),
            _ => TryConvertOther(text, targetType, out value),
        };

        if (!ok)
        {
            error ??= $"{FriendlyTypeName(targetType)} として解釈できません (value = {text})";
        }
        return ok;
    }

    private static bool Parse<T>(bool success, T parsed, out object? value)
    {
        value = success ? parsed : null;
        return success;
    }

    private static bool TryConvertOther(string text, Type targetType, out object? value)
    {
        if (targetType == typeof(Guid) && Guid.TryParse(text, out var guid))
        {
            value = guid;
            return true;
        }
        if (targetType == typeof(TimeSpan) && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span))
        {
            value = span;
            return true;
        }
        if (targetType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            value = dto;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryConvertArray(string cell, Type elementType, out object? value, out string? error)
    {
        error = null;
        if (cell.Trim().Length == 0)
        {
            value = Array.CreateInstance(elementType, 0);
            return true;
        }

        var parts = cell.Split(ArraySeparator);
        var array = Array.CreateInstance(elementType, parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryConvert(parts[i].Trim(), elementType, out var element, out var elementError))
            {
                value = null;
                error = $"{i + 1} 番目の要素: {elementError}";
                return false;
            }
            array.SetValue(element, i);
        }

        value = array;
        return true;
    }

    private static bool TryConvertEnum(string text, Type enumType, out object? value, out string? error)
    {
        error = null;
        if (Enum.TryParse(enumType, text, ignoreCase: true, out var parsed) && parsed is not null)
        {
            var isNumeric = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            var isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);
            if (isNumeric && !isFlags && !Enum.IsDefined(enumType, parsed))
            {
                value = null;
                error = $"enum {enumType.Name} に定義されていない数値です (value = {text})";
                return false;
            }
            value = parsed;
            return true;
        }

        value = null;
        error = $"enum {enumType.Name} に該当する名前または数値ではありません (value = {text})";
        return false;
    }

    public static string FriendlyTypeName(Type type)
    {
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        return type.Name;
    }
}
