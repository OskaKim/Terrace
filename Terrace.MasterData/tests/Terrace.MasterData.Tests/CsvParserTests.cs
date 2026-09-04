using Terrace.MasterData.Builder.Csv;
using Terrace.MasterData.Builder.Diagnostics;

namespace Terrace.MasterData.Tests;

public class CsvParserTests
{
    [Fact]
    public void コメント行と空行を読み飛ばしつつ物理行番号を保持する()
    {
        var diagnostics = new DiagnosticBag();
        var doc = CsvParser.Parse("item.csv", "# comment\nItemId,Name\n\n1,a\n# another\n2,b\n", diagnostics);

        Assert.False(diagnostics.HasErrors);
        Assert.Equal(2, doc.HeaderLine);
        Assert.Equal(new[] { "ItemId", "Name" }, doc.Header);
        Assert.Equal(new[] { 4, 6 }, doc.Rows.Select(r => r.Line).ToArray());
        Assert.Equal(new[] { "2", "b" }, doc.Rows[1].Cells);
    }

    [Fact]
    public void 引用符付きセルはカンマと二重引用符を含められる()
    {
        var ok = CsvParser.TrySplitLine("1,\"a, b\",\"say \"\"hi\"\"\", c ", out var cells, out var error);

        Assert.True(ok, error);
        Assert.Equal(new[] { "1", "a, b", "say \"hi\"", "c" }, cells);
    }

    [Fact]
    public void 末尾のカンマは空セルになる()
    {
        Assert.True(CsvParser.TrySplitLine("1,Slime,", out var cells, out _));
        Assert.Equal(new[] { "1", "Slime", "" }, cells);
    }

    [Fact]
    public void 閉じていない引用符はエラー()
    {
        var diagnostics = new DiagnosticBag();
        var doc = CsvParser.Parse("item.csv", "ItemId,Name\n1,\"broken\n2,ok\n", diagnostics);

        var error = Assert.Single(diagnostics.Errors);
        Assert.Equal("item.csv:2", error.Location);
        Assert.Single(doc.Rows);
    }

    [Fact]
    public void ヘッダより列数が多い行はエラーで少ない行は空セルで補う()
    {
        var diagnostics = new DiagnosticBag();
        var doc = CsvParser.Parse("item.csv", "A,B,C\n1,2,3,4\n5\n", diagnostics);

        var error = Assert.Single(diagnostics.Errors);
        Assert.Equal("item.csv:2", error.Location);
        var row = Assert.Single(doc.Rows);
        Assert.Equal(new[] { "5", "", "" }, row.Cells);
    }

    [Fact]
    public void BOMを取り除く()
    {
        var diagnostics = new DiagnosticBag();
        var doc = CsvParser.Parse("item.csv", (char)0xFEFF + "ItemId,Name\n1,a\n", diagnostics);

        Assert.Equal("ItemId", doc.Header[0]);
    }

    [Fact]
    public void ヘッダに同名の列があればエラー()
    {
        var diagnostics = new DiagnosticBag();
        CsvParser.Parse("item.csv", "ItemId,name,Name\n1,a,b\n", diagnostics);

        var error = Assert.Single(diagnostics.Errors);
        Assert.Equal("name", error.Column);
    }

    [Fact]
    public void CRLFでもLFでも行番号は同じ()
    {
        var diagnostics = new DiagnosticBag();
        var crlf = CsvParser.Parse("item.csv", "ItemId,Name\r\n1,a\r\n\r\n2,b\r\n", diagnostics);
        var lf = CsvParser.Parse("item.csv", "ItemId,Name\n1,a\n\n2,b\n", diagnostics);

        Assert.Equal(new[] { 2, 4 }, crlf.Rows.Select(r => r.Line).ToArray());
        Assert.Equal(new[] { 2, 4 }, lf.Rows.Select(r => r.Line).ToArray());
    }
}
