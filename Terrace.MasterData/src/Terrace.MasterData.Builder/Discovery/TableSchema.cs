using System.Reflection;
using MasterMemory;
using MessagePack;

namespace Terrace.MasterData.Builder.Discovery;

/// <summary>[MemoryTable] が付いた 1 型のスキーマ。CSV の列とプロパティの対応付けに使う。</summary>
public sealed class TableSchema
{
    public TableSchema(Type type, string tableName)
    {
        Type = type;
        TableName = tableName;

        Properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetSetMethod() is not null && p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<IgnoreMemberAttribute>() is null)
            .ToArray();

        PrimaryKeyProperties = Properties
            .Select(p => (Property: p, Attribute: p.GetCustomAttribute<PrimaryKeyAttribute>()))
            .Where(x => x.Attribute is not null)
            .OrderBy(x => x.Attribute!.KeyOrder)
            .Select(x => x.Property)
            .ToArray();

        PrimaryKeyName = string.Join(",", PrimaryKeyProperties.Select(p => p.Name));
    }

    public Type Type { get; }

    /// <summary>MemoryTable 名。規約により CSV ファイル名(拡張子なし)と一致させる。</summary>
    public string TableName { get; }

    public string CsvFileName => TableName + ".csv";

    /// <summary>CSV からマッピングする対象(public な get/set プロパティ)。</summary>
    public IReadOnlyList<PropertyInfo> Properties { get; }

    public IReadOnlyList<PropertyInfo> PrimaryKeyProperties { get; }

    /// <summary>"ItemId" または複合キーなら "A,B"。MasterMemory の失敗メッセージと突き合わせる。</summary>
    public string PrimaryKeyName { get; }

    public PropertyInfo? FindProperty(string name)
        => Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool TryGetPrimaryKey(object row, out PrimaryKeyValue key)
    {
        if (PrimaryKeyProperties.Count == 0)
        {
            key = null!;
            return false;
        }

        key = new PrimaryKeyValue(PrimaryKeyProperties.Select(p => p.GetValue(row)).ToArray());
        return true;
    }
}
