using Terrace.MasterData.Builder.Mapping;
using Terrace.MasterData.Tables;

namespace Terrace.MasterData.Tests;

public class ValueConverterTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("0", false)]
    [InlineData(" true ", true)]
    public void boolの表記ゆれを受け付ける(string cell, bool expected)
    {
        Assert.True(ValueConverter.TryConvert(cell, typeof(bool), out var value, out var error), error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("2")]
    [InlineData("t")]
    public void boolとして解釈できない値はエラー(string cell)
    {
        Assert.False(ValueConverter.TryConvert(cell, typeof(bool), out _, out var error));
        Assert.Contains("bool", error);
        Assert.Contains($"value = {cell}", error);
    }

    [Fact]
    public void 配列はパイプ区切りで空セルは空配列()
    {
        Assert.True(ValueConverter.TryConvert("1|2|3", typeof(int[]), out var value, out _));
        Assert.Equal(new[] { 1, 2, 3 }, (int[])value!);

        Assert.True(ValueConverter.TryConvert(" 4 | 5 ", typeof(int[]), out value, out _));
        Assert.Equal(new[] { 4, 5 }, (int[])value!);

        Assert.True(ValueConverter.TryConvert("", typeof(int[]), out value, out _));
        Assert.Empty((int[])value!);

        Assert.True(ValueConverter.TryConvert("a|b", typeof(string[]), out value, out _));
        Assert.Equal(new[] { "a", "b" }, (string[])value!);

        Assert.True(ValueConverter.TryConvert("Weapon|2", typeof(ItemCategory[]), out value, out _));
        Assert.Equal(new[] { ItemCategory.Weapon, ItemCategory.Armor }, (ItemCategory[])value!);
    }

    [Fact]
    public void 配列の要素が不正なら何番目かを添えてエラー()
    {
        Assert.False(ValueConverter.TryConvert("1|x|3", typeof(int[]), out _, out var error));
        Assert.Contains("2 番目", error);
        Assert.Contains("value = x", error);
    }

    [Theory]
    [InlineData("Weapon", ItemCategory.Weapon)]
    [InlineData("weapon", ItemCategory.Weapon)]
    [InlineData("ARMOR", ItemCategory.Armor)]
    [InlineData("3", ItemCategory.Consumable)]
    [InlineData("", ItemCategory.None)]
    public void enumは名前でも数値でも指定できる(string cell, ItemCategory expected)
    {
        Assert.True(ValueConverter.TryConvert(cell, typeof(ItemCategory), out var value, out var error), error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("Wepon")]
    [InlineData("99")]
    public void enumに無い名前や数値はエラー(string cell)
    {
        Assert.False(ValueConverter.TryConvert(cell, typeof(ItemCategory), out _, out var error));
        Assert.Contains("ItemCategory", error);
    }

    [Fact]
    public void 空セルはその型のdefaultになる()
    {
        Assert.True(ValueConverter.TryConvert("", typeof(int), out var i, out _));
        Assert.Equal(0, i);
        Assert.True(ValueConverter.TryConvert("", typeof(bool), out var b, out _));
        Assert.Equal(false, b);
        Assert.True(ValueConverter.TryConvert("", typeof(float), out var f, out _));
        Assert.Equal(0f, f);
        Assert.True(ValueConverter.TryConvert("", typeof(string), out var s, out _));
        Assert.Equal("", s);
        Assert.True(ValueConverter.TryConvert("", typeof(int?), out var n, out _));
        Assert.Null(n);
        Assert.True(ValueConverter.TryConvert("7", typeof(int?), out n, out _));
        Assert.Equal(7, n);
    }

    [Theory]
    [InlineData("300", typeof(int), 300)]
    [InlineData("-5", typeof(int), -5)]
    [InlineData("1.5", typeof(float), 1.5f)]
    [InlineData("2.25", typeof(double), 2.25)]
    [InlineData("9000000000", typeof(long), 9000000000L)]
    public void 数値を変換できる(string cell, Type type, object expected)
    {
        Assert.True(ValueConverter.TryConvert(cell, type, out var value, out var error), error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc", typeof(int))]
    [InlineData("1.5", typeof(int))]
    [InlineData("x", typeof(float))]
    public void 数値として解釈できない値はエラー(string cell, Type type)
    {
        Assert.False(ValueConverter.TryConvert(cell, type, out _, out var error));
        Assert.Contains($"value = {cell}", error);
    }
}
