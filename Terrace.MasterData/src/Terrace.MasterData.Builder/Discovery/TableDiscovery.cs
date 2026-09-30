using System.Reflection;
using MasterMemory;

namespace Terrace.MasterData.Builder.Discovery;

/// <summary>
/// [MemoryTable] 属性の付いた型をアセンブリからリフレクションで自動収集する。
/// 「追加するたびに編集する一覧ファイル」は作らない。
/// </summary>
public static class TableDiscovery
{
    public static IReadOnlyList<TableSchema> Discover(Assembly assembly)
    {
        var schemas = assembly
            .GetTypes()
            .Select(t => (Type: t, Attribute: t.GetCustomAttribute<MemoryTableAttribute>()))
            .Where(x => x.Attribute is not null && !x.Type.IsAbstract)
            .Select(x => new TableSchema(x.Type, x.Attribute!.TableName))
            .OrderBy(s => s.TableName, StringComparer.Ordinal)
            .ToList();

        var duplicated = schemas
            .GroupBy(s => s.TableName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException(
                $"MemoryTable 名 '{duplicated.Key}' が複数の型で使われています: {string.Join(", ", duplicated.Select(s => s.Type.FullName))}");
        }

        return schemas;
    }

    /// <summary>Shared を取り込んでいるこのアセンブリ(Builder 自身)から収集する。</summary>
    public static IReadOnlyList<TableSchema> DiscoverDefault() => Discover(typeof(TableDiscovery).Assembly);
}
