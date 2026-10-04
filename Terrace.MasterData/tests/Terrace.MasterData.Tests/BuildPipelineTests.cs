using Terrace.MasterData.Builder.Build;
using Terrace.MasterData.Builder.Output;
using Terrace.MasterData.Tables;

namespace Terrace.MasterData.Tests;

public class BuildPipelineTests
{
    [Fact]
    public void 正常系_3テーブルを変換して読み戻すと件数と代表値が一致する()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("valid"));

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.All));
        Assert.Empty(result.Diagnostics.All);

        var db = new MemoryDatabase(result.Binary!);
        Assert.Equal(5, db.ItemTable.Count);
        Assert.Equal(3, db.QuestTable.Count);
        Assert.Equal(3, db.EnemyTable.Count);

        Assert.Equal("Shield, Large", db.ItemTable.FindByItemId(3).Name);
        Assert.Equal(500, db.ItemTable.FindByItemId(3).Price);
        Assert.Equal(ItemCategory.Material, db.ItemTable.FindByItemId(4).Category);
        Assert.Equal(ItemCategory.Material, db.ItemTable.FindByItemId(5).Category);
        Assert.Equal("はじまりの依頼", db.QuestTable.FindByQuestId(1).Name);
        Assert.Equal(99, db.QuestTable.FindByQuestId(3).RequiredLevel);
        Assert.Equal(new[] { 2, 3, 4 }, db.EnemyTable.FindByEnemyId(2).DropItemIds);
        Assert.Empty(db.EnemyTable.FindByEnemyId(3).DropItemIds);
        Assert.Equal(10, db.EnemyTable.FindByEnemyId(2).Exp);

        Assert.Equal(3, db.PlayerLevelTable.Count);
        Assert.Equal(15, db.PlayerLevelTable.FindByLevel(1).ExpToNext);
        Assert.Equal(110, db.PlayerLevelTable.FindByLevel(2).MaxHp);
        Assert.Equal(12, db.PlayerLevelTable.FindByLevel(3).Attack);
        Assert.Equal(0, db.PlayerLevelTable.FindByLevel(3).ExpToNext);
    }

    [Fact]
    public void 正常系_manifestにハッシュと件数が入る()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("valid"));

        var manifest = result.Manifest!;
        Assert.Equal(Manifest.ComputeSha256(result.Binary!), manifest.Sha256);
        Assert.Equal(result.Binary!.Length, manifest.ByteLength);
        Assert.True(DateTimeOffset.TryParse(manifest.GeneratedAt, out _));
        Assert.Equal(new[] { ("enemy", 3), ("item", 5), ("player_level", 3), ("quest", 3) }, manifest.Tables.Select(t => (t.Name, t.Count)).ToArray());
        Assert.Equal(typeof(Item).FullName, manifest.Tables.Single(t => t.Name == "item").Type);

        var roundTrip = Manifest.FromJson(manifest.ToJson());
        Assert.Equal(manifest.Sha256, roundTrip.Sha256);
        Assert.Equal(4, roundTrip.Tables.Count);
    }

    [Fact]
    public void 異常系_主キー重複はファイル名と行番号つきで報告する()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("duplicate_pk"));

        Assert.False(result.Success);
        Assert.False(result.ValidationSkipped);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("item.csv:5", error.Location);
        Assert.Equal("ItemId", error.Column);
        Assert.Contains("主キーが重複しています", error.Message);
        Assert.Contains("value = 2", error.Message);
        Assert.Contains("item.csv:3", error.Message);
        Assert.StartsWith("item.csv:5  [ItemId] 主キーが重複しています", error.Format());
    }

    [Fact]
    public void 異常系_参照切れと範囲外はファイル名と行番号つきで報告する()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("broken_reference"));

        Assert.False(result.Success);
        var lines = result.Diagnostics.Errors.Select(e => e.Format()).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Contains("quest.csv:3  [RewardItemId] item.csv に存在しないIDを参照しています (value = 999)", lines);
        Assert.Contains("quest.csv:4  [RequiredLevel] 1〜99 の範囲で指定してください (value = 0)", lines);
        Assert.Contains("enemy.csv:2  [DropItemIds] item.csv に存在しないIDを参照しています (value = 999)", lines);
    }

    [Fact]
    public void 異常系_敵の経験値が負ならExp列のエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "enemy.csv"),
            "EnemyId,Name,Hp,Attack,Exp,DropItemIds\n1,Slime,10,1,3,1\n2,Goblin,30,5,-1,2\n");

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("enemy.csv:3  [Exp] 0 以上で指定してください (value = -1)", error.Format());
    }

    [Fact]
    public void 異常系_レベルが欠けていれば欠けの上の行でLevel列のエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "player_level.csv"),
            "Level,ExpToNext,MaxHp,Attack\n1,15,100,10\n2,30,110,11\n4,0,130,13\n");

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("player_level.csv:4", error.Location);
        Assert.Equal("Level", error.Column);
        Assert.Contains("Level 3 の行がありません", error.Message);
    }

    [Fact]
    public void 異常系_レベル1が無ければLevel列のエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "player_level.csv"),
            "Level,ExpToNext,MaxHp,Attack\n2,30,110,11\n3,0,120,12\n");

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("player_level.csv:2", error.Location);
        Assert.Equal("Level", error.Column);
        Assert.Contains("Level 1 の行がありません", error.Message);
    }

    [Fact]
    public void 異常系_途中の行のExpToNextが0ならExpToNext列のエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "player_level.csv"),
            "Level,ExpToNext,MaxHp,Attack\n1,15,100,10\n2,0,110,11\n3,0,120,12\n");

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("player_level.csv:3  [ExpToNext] 最高レベル以外の行は 1 以上で指定してください (value = 0)", error.Format());
    }

    [Fact]
    public void 異常系_最高レベルの行のExpToNextが0でなければExpToNext列のエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "player_level.csv"),
            "Level,ExpToNext,MaxHp,Attack\n1,15,100,10\n2,30,110,11\n");

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("player_level.csv:3  [ExpToNext] 最高レベルの行は 0 にしてください (value = 30)", error.Format());
    }

    [Fact]
    public void 異常系_レベルの最大HPと攻撃力が0ならそれぞれの列のエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "player_level.csv"),
            "Level,ExpToNext,MaxHp,Attack\n1,15,0,10\n2,0,110,0\n");

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var lines = result.Diagnostics.Errors.Select(e => e.Format()).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Contains("player_level.csv:2  [MaxHp] 1 以上で指定してください (value = 0)", lines);
        Assert.Contains("player_level.csv:3  [Attack] 1 以上で指定してください (value = 0)", lines);
    }

    [Fact]
    public void 異常系_型不正はファイル名と行番号と列名つきで報告し検証はスキップする()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("invalid_type"));

        Assert.False(result.Success);
        Assert.True(result.ValidationSkipped);
        var errors = result.Diagnostics.Errors.ToList();
        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, e => e.Location == "item.csv:3" && e.Column == "Price" && e.Message.Contains("value = abc"));
        Assert.Contains(errors, e => e.Location == "item.csv:4" && e.Column == "Category" && e.Message.Contains("value = Wepon"));
        Assert.Contains(errors, e => e.Location == "item.csv:5" && e.Column == "ItemId" && e.Message.Contains("value = x"));
    }

    [Fact]
    public void 異常系_列が足りなければエラー()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("missing_column"));

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("item.csv:1", error.Location);
        Assert.Equal("Price", error.Column);
    }

    [Fact]
    public void 未知の列は警告だけでビルドは成功する()
    {
        var result = BuildPipeline.Run(Fixtures.Dir("unknown_column"));

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.All));
        var warning = Assert.Single(result.Diagnostics.Warnings);
        Assert.Equal("Memo", warning.Column);
        Assert.Equal("item.csv:1", warning.Location);
    }

    [Fact]
    public void 異常系_テーブルに対応するCSVが無ければエラー()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.Delete(Path.Combine(dir, "enemy.csv"));

        var result = BuildPipeline.Run(dir);

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics.Errors);
        Assert.Equal("enemy.csv", error.FileName);
        Assert.Contains("Enemy", error.Message);
    }

    [Fact]
    public void テーブル定義の無いCSVは警告として無視する()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.WriteAllText(Path.Combine(dir, "unknown.csv"), "A,B\n1,2\n");

        var result = BuildPipeline.Run(dir);

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.All));
        var warning = Assert.Single(result.Diagnostics.Warnings);
        Assert.Equal("unknown.csv", warning.FileName);
    }

    [Fact]
    public void CSVファイル名は大文字小文字を区別しない()
    {
        var dir = Fixtures.CopyToTemp("valid");
        File.Move(Path.Combine(dir, "item.csv"), Path.Combine(dir, "ITEM.csv"));

        var result = BuildPipeline.Run(dir);

        Assert.True(result.Success, string.Join("\n", result.Diagnostics.All));
    }
}
