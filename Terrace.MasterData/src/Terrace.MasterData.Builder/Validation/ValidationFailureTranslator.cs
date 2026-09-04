using System.Text.RegularExpressions;
using MasterMemory.Validation;
using Terrace.MasterData.Builder.Diagnostics;
using Terrace.MasterData.Builder.Discovery;
using Terrace.MasterData.Builder.Tracing;

namespace Terrace.MasterData.Builder.Validation;

/// <summary>
/// MasterMemory の Validate が返す失敗(オブジェクト単位)を、CSV のファイル名・行番号・列名へ逆引きして
/// <see cref="Diagnostic"/> に翻訳する。
///
/// MasterMemory 側の書式(MasterMemory 3.0 時点):
///   Unique failed: ItemId, value = 2                                      … 主キー重複(TableBase.ValidateUniqueCore)
///   Unique failed: x.Email, value = a, PK(ItemId) = 1                     … ValidatableSet.Unique
///   Exists failed: Quest.RewardItemId -> Item.ItemId, value = 999, PK(QuestId) = 2
///   Validate failed: {メッセージ}, PK(QuestId) = 2                           … IValidator.Validate(func, message)
///   Validate failed: {式}, {メンバ値}, PK(QuestId) = 2                       … IValidator.Validate(expression)
///   {メッセージ}, PK(QuestId) = 2                                            … IValidator.Fail
/// 独自メッセージは "[列名] 本文" の形で書くと列名として拾う。
/// </summary>
public sealed class ValidationFailureTranslator
{
    private static readonly Regex PkSuffix = new(@"^(?<body>.*),\s*PK\((?<pkName>[^)]*)\)\s*=\s*(?<pkValue>.*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex UniqueFailed = new(@"^Unique failed:\s*(?<member>.+?)\s*,\s*value = (?<value>.*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ExistsFailed = new(@"^Exists failed:\s*(?<from>\S+)\s*->\s*(?<to>[^,]+?)\s*,\s*value = (?<value>.*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ValidateFailed = new(@"^Validate(Action)? failed:\s*(?<rest>.*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ColumnPrefix = new(@"^\[(?<column>[A-Za-z_][A-Za-z0-9_]*)\]\s*(?<rest>.*)$", RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly RowTraceRegistry _registry;
    private readonly Dictionary<Type, TableSchema> _byType;
    private readonly Dictionary<string, TableSchema> _byTypeName;

    public ValidationFailureTranslator(RowTraceRegistry registry, IEnumerable<TableSchema> schemas)
    {
        _registry = registry;
        _byType = schemas.ToDictionary(s => s.Type);
        _byTypeName = _byType.Values
            .GroupBy(s => s.Type.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    public void Translate(FaildItem failure, DiagnosticBag diagnostics)
    {
        _byType.TryGetValue(failure.Type, out var schema);
        var (message, pkValue) = StripPkSuffix(failure.Message);
        var interpreted = Interpret(message, schema);
        var locations = failure.Data is null ? Array.Empty<SourceLocation>() : _registry.Resolve(failure.Data);

        if (interpreted.IsPrimaryKeyDuplicate && locations.Count > 1)
        {
            var first = locations[0];
            foreach (var location in locations.Skip(1))
            {
                diagnostics.Error(location.FileName, location.Line, interpreted.Column,
                    $"{interpreted.Text} (value = {interpreted.Value}, 初出 {first})");
            }
            return;
        }

        var text = interpreted.Value is null ? interpreted.Text : $"{interpreted.Text} (value = {interpreted.Value})";

        if (locations.Count == 0)
        {
            var pk = pkValue ?? (failure.Data is not null && schema is not null && schema.TryGetPrimaryKey(failure.Data, out var key) ? key.ToString() : null);
            var hint = pk is null ? "行を特定できません" : $"行を特定できません。PK = {pk}";
            diagnostics.Error(schema?.CsvFileName, null, interpreted.Column, $"{text} ({hint})");
            return;
        }

        foreach (var location in locations)
        {
            diagnostics.Error(location.FileName, location.Line, interpreted.Column, text);
        }
    }

    private static (string Message, string? PkValue) StripPkSuffix(string message)
    {
        var m = PkSuffix.Match(message);
        return m.Success ? (m.Groups["body"].Value.Trim(), m.Groups["pkValue"].Value.Trim()) : (message.Trim(), null);
    }

    private readonly record struct Interpretation(string? Column, string Text, string? Value, bool IsPrimaryKeyDuplicate);

    private Interpretation Interpret(string message, TableSchema? schema)
    {
        var unique = UniqueFailed.Match(message);
        if (unique.Success)
        {
            var member = LastSegment(unique.Groups["member"].Value);
            var value = unique.Groups["value"].Value.Trim();
            var isPrimaryKey = schema is not null && string.Equals(member, schema.PrimaryKeyName, StringComparison.Ordinal);
            return isPrimaryKey
                ? new Interpretation(member, "主キーが重複しています", value, true)
                : new Interpretation(member, "値が重複しています", value, false);
        }

        var exists = ExistsFailed.Match(message);
        if (exists.Success)
        {
            var from = exists.Groups["from"].Value;
            var to = exists.Groups["to"].Value.Trim();
            var value = exists.Groups["value"].Value.Trim();
            var column = LastSegment(from);
            var referencedTypeName = to.Contains('.') ? to[..to.IndexOf('.')] : to;
            var text = _byTypeName.TryGetValue(referencedTypeName, out var referenced)
                ? $"{referenced.CsvFileName} に存在しないIDを参照しています"
                : $"{to} に存在しない値を参照しています";
            return new Interpretation(column, text, value, false);
        }

        var validate = ValidateFailed.Match(message);
        if (validate.Success)
        {
            var rest = validate.Groups["rest"].Value.Trim();
            var prefixed = ColumnPrefix.Match(rest);
            return prefixed.Success
                ? new Interpretation(prefixed.Groups["column"].Value, prefixed.Groups["rest"].Value.Trim(), null, false)
                : new Interpretation(null, $"検証に失敗しました: {rest}", null, false);
        }

        var custom = ColumnPrefix.Match(message);
        return custom.Success
            ? new Interpretation(custom.Groups["column"].Value, custom.Groups["rest"].Value.Trim(), null, false)
            : new Interpretation(null, message, null, false);
    }

    private static string LastSegment(string memberPath)
    {
        var trimmed = memberPath.Trim();
        var dot = trimmed.LastIndexOf('.');
        return dot < 0 ? trimmed : trimmed[(dot + 1)..];
    }
}
