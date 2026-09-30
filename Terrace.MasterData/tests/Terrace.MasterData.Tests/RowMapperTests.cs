using Terrace.MasterData.Builder.Csv;
using Terrace.MasterData.Builder.Diagnostics;
using Terrace.MasterData.Builder.Discovery;
using Terrace.MasterData.Builder.Mapping;
using Terrace.MasterData.Builder.Tracing;
using Terrace.MasterData.Tables;

namespace Terrace.MasterData.Tests;

public class RowMapperTests
{
    private static (IReadOnlyList<object> Rows, DiagnosticBag Diagnostics, RowTraceRegistry Registry) Map(string csv)
    {
        var schema = new TableSchema(typeof(Item), "item");
        var diagnostics = new DiagnosticBag();
        var registry = new RowTraceRegistry();
        var doc = CsvParser.Parse(Path.Combine("some", "dir", "item.csv"), csv, diagnostics);
        var rows = RowMapper.Map(schema, doc, registry, diagnostics);
        return (rows, diagnostics, registry);
    }

    [Fact]
    public void ヘッダ名は大文字小文字を区別せずプロパティに対応付ける()
    {
        var (rows, diagnostics, _) = Map("itemid,NAME,price,category\n1,Potion,50,Consumable\n");

        Assert.False(diagnostics.HasErrors);
        var item = Assert.IsType<Item>(Assert.Single(rows));
        Assert.Equal(1, item.ItemId);
        Assert.Equal("Potion", item.Name);
        Assert.Equal(50, item.Price);
        Assert.Equal(ItemCategory.Consumable, item.Category);
    }

    [Fact]
    public void 列の順序はヘッダに従う()
    {
        var (rows, diagnostics, _) = Map("Category,Price,Name,ItemId\nWeapon,300,Sword,2\n");

        Assert.False(diagnostics.HasErrors);
        var item = Assert.IsType<Item>(Assert.Single(rows));
        Assert.Equal(2, item.ItemId);
        Assert.Equal("Sword", item.Name);
        Assert.Equal(300, item.Price);
        Assert.Equal(ItemCategory.Weapon, item.Category);
    }

    [Fact]
    public void 未知の列は警告として報告し処理は続行する()
    {
        var (rows, diagnostics, _) = Map("ItemId,Name,Price,Category,Memo\n1,Potion,50,Consumable,note\n");

        Assert.False(diagnostics.HasErrors);
        var warning = Assert.Single(diagnostics.Warnings);
        Assert.Equal("Memo", warning.Column);
        Assert.Equal("item.csv:1", warning.Location);
        Assert.Single(rows);
    }

    [Fact]
    public void 型定義にある列がCSVに無ければエラー()
    {
        var (rows, diagnostics, _) = Map("ItemId,Name,Category\n1,Potion,Consumable\n");

        var error = Assert.Single(diagnostics.Errors);
        Assert.Equal("Price", error.Column);
        Assert.Equal("item.csv:1", error.Location);
        Assert.Empty(rows);
    }

    [Fact]
    public void 型変換に失敗した行は除外し他の行は写す()
    {
        var (rows, diagnostics, _) = Map("ItemId,Name,Price,Category\n1,Potion,50,Consumable\n2,Sword,abc,Weapon\n3,Shield,500,Armor\n");

        var error = Assert.Single(diagnostics.Errors);
        Assert.Equal("item.csv:3", error.Location);
        Assert.Equal("Price", error.Column);
        Assert.Equal(new[] { 1, 3 }, rows.Cast<Item>().Select(x => x.ItemId).ToArray());
    }

    [Fact]
    public void 生成したオブジェクトの由来を台帳に登録する()
    {
        var (rows, _, registry) = Map("# c\nItemId,Name,Price,Category\n\n1,Potion,50,Consumable\n2,Sword,300,Weapon\n");

        var byReference = registry.Resolve(rows[1]);
        Assert.Equal("item.csv:5", Assert.Single(byReference).ToString());

        // 別インスタンス(バイナリから読み戻した想定)でも主キーで引ける
        var copy = new Item { ItemId = 1 };
        Assert.Equal("item.csv:4", Assert.Single(registry.Resolve(copy)).ToString());

        Assert.Empty(registry.Resolve(new Item { ItemId = 999 }));
    }

    [Fact]
    public void 主キーが重複していれば該当する全行を返す()
    {
        var (_, _, registry) = Map("ItemId,Name,Price,Category\n1,A,1,Weapon\n2,B,1,Weapon\n1,C,1,Weapon\n");

        var locations = registry.Resolve(new Item { ItemId = 1 });
        Assert.Equal(new[] { "item.csv:2", "item.csv:4" }, locations.Select(l => l.ToString()).ToArray());
    }
}
