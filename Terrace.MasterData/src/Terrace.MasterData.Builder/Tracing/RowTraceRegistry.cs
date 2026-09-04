using Terrace.MasterData.Builder.Discovery;

namespace Terrace.MasterData.Builder.Tracing;

/// <summary>
/// 「どのオブジェクトがどのファイルの何行目の由来か」を保持する台帳。
///
/// 逆引きは 2 段構え:
/// 1. 参照同一性(CSV から生成したそのインスタンス)
/// 2. 主キー値(MasterMemory の Validate はバイナリから読み戻した別インスタンスを返すため)
/// 主キーが重複している場合は該当する全行を返す。
/// </summary>
public sealed class RowTraceRegistry
{
    private readonly Dictionary<object, SourceLocation> _byReference = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Type, TableSchema> _schemas = new();
    private readonly Dictionary<(Type Type, PrimaryKeyValue Key), List<SourceLocation>> _byPrimaryKey = new();

    public int Count => _byReference.Count;

    public void Register(TableSchema schema, object row, SourceLocation location)
    {
        _byReference[row] = location;
        _schemas.TryAdd(schema.Type, schema);

        if (schema.TryGetPrimaryKey(row, out var key))
        {
            if (!_byPrimaryKey.TryGetValue((schema.Type, key), out var list))
            {
                list = new List<SourceLocation>();
                _byPrimaryKey[(schema.Type, key)] = list;
            }
            list.Add(location);
        }
    }

    public bool TryResolveByReference(object row, out SourceLocation location)
        => _byReference.TryGetValue(row, out location);

    public IReadOnlyList<SourceLocation> ResolveByPrimaryKey(Type type, PrimaryKeyValue key)
        => _byPrimaryKey.TryGetValue((type, key), out var list) ? list : Array.Empty<SourceLocation>();

    /// <summary>参照同一性 → 主キー の順で由来を引く。見つからなければ空。</summary>
    public IReadOnlyList<SourceLocation> Resolve(object row)
    {
        if (TryResolveByReference(row, out var location))
        {
            return new[] { location };
        }

        var type = row.GetType();
        if (_schemas.TryGetValue(type, out var schema) && schema.TryGetPrimaryKey(row, out var key))
        {
            return ResolveByPrimaryKey(type, key);
        }

        return Array.Empty<SourceLocation>();
    }
}
