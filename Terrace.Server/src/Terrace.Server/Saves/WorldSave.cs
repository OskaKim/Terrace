using System.Text.Json;
using System.Text.Json.Serialization;
using Terrace.Server.Accounts;

namespace Terrace.Server.Saves;

/// <summary>ワールドのファイルの中身(world.json)。形は docs/contracts/world-save.md。</summary>
public sealed class WorldSaveFile
{
    /// <summary>この Server が読み書きできる形式の版。形を変えたら上げ、古い版からの移し方を <see cref="WorldSave"/> に足す。</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string WorldId { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;

    /// <summary>最後に発行したプレイヤー ID。</summary>
    public int LastPlayerId { get; set; }

    public List<AccountRecord> Accounts { get; set; } = new();
}

/// <summary>キャラクター 1 人の保存の形。トークンそのものは書かない。</summary>
public sealed class AccountRecord
{
    public int PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenSha256 { get; set; } = string.Empty;
}

/// <summary>
/// ワールドのフォルダへの保存(<c>TerraceServerOptions.WorldDirectory</c>)。今は 1 つの JSON ファイル。
///
/// - 起動のときに読む。無ければ新しいワールド(新しい ID、名前は渡された既定の名前)を作ってすぐ書く
/// - 知らない版のファイルや壊れたファイルなら、例外で起動を止める(上書きして消さない)
/// - 書くときは別のファイルに書いてから置き換える(途中で落ちても前のファイルが残る)
/// - どこから呼ばれても 1 度に 1 つずつ書く
/// </summary>
public sealed class WorldSave(string directory, AccountRegistry accounts)
{
    public const string FileName = "world.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly object _gate = new();

    public string Directory { get; } = directory;

    public string FilePath => Path.Combine(Directory, FileName);

    /// <summary>読んで、キャラクターを台帳に戻す。無ければ新しく作って書く。返すのはこのワールド。</summary>
    public WorldIdentity LoadOrCreate(string defaultName)
    {
        var file = Read();
        if (file is null)
        {
            var created = new WorldIdentity(defaultName);
            Write(created);
            return created;
        }

        accounts.Restore(file.Accounts.Select(a => new Account(a.PlayerId, a.Name, a.TokenSha256)), file.LastPlayerId);
        return new WorldIdentity(file.WorldName, file.WorldId);
    }

    /// <summary>今の台帳を書く。</summary>
    public void Write(WorldIdentity world)
    {
        var file = new WorldSaveFile
        {
            WorldId = world.Id,
            WorldName = world.Name,
            LastPlayerId = accounts.LastPlayerId,
            Accounts = accounts.Accounts.Select(a => new AccountRecord { PlayerId = a.PlayerId, Name = a.Name, TokenSha256 = a.TokenSha256 }).ToList(),
        };

        lock (_gate)
        {
            System.IO.Directory.CreateDirectory(Directory);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(file, Json));
            File.Move(temporary, FilePath, overwrite: true);
        }
    }

    /// <summary>ファイルを読む。無ければ null。知らない版や壊れたファイルなら InvalidDataException。</summary>
    public WorldSaveFile? Read()
    {
        if (!File.Exists(FilePath)) return null;

        WorldSaveFile? file;
        try
        {
            file = JsonSerializer.Deserialize<WorldSaveFile>(File.ReadAllText(FilePath), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{FilePath} を読めません(壊れています): {ex.Message}", ex);
        }

        if (file is null || string.IsNullOrEmpty(file.WorldId)) throw new InvalidDataException($"{FilePath} にワールドの ID がありません");
        if (file.Version != WorldSaveFile.CurrentVersion)
        {
            throw new InvalidDataException($"{FilePath} の版 {file.Version} は、この Server(版 {WorldSaveFile.CurrentVersion})では読めません");
        }
        return file;
    }
}
