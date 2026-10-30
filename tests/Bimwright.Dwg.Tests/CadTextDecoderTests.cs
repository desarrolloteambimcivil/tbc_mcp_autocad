#nullable enable
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Bimwright.Dwg.Plugin.Cad;
using Xunit;

namespace Bimwright.Dwg.Tests;

public sealed class CadTextDecoderTests
{
    [Theory]
    [InlineData("1T %%C90 ALIVIADERO", "1T Ø90 ALIVIADERO")]
    [InlineData("%%c315 PVC", "Ø315 PVC")]
    [InlineData("2T%%c250 PVC", "2TØ250 PVC")]
    [InlineData("45%%d", "45°")]
    [InlineData("%%p0.05", "±0.05")]
    [InlineData("100%%%", "100%")]
    [InlineData("%%uSUBRAYADO%%u", "SUBRAYADO")]
    [InlineData("%%216", "Ø")]
    [InlineData("\\U+2205160", "∅160")]
    [InlineData("Z ARQUETA:+10.41", "Z ARQUETA:+10.41")]
    [InlineData("50%", "50%")]
    [InlineData("%%x", "%%x")]
    [InlineData("", "")]
    public void Plain_decodes_control_codes_and_unicode_escapes(string raw, string expected)
        => Assert.Equal(expected, CadTextDecoder.Plain(raw));

    [Fact]
    public void Plain_keeps_null()
        => Assert.Null(CadTextDecoder.Plain(null!));

    [Theory]
    [InlineData("CONEXI\\U+00D3N ASEOS\\PEDIFICIO P\\U+00DABLICO", "CONEXIÓN ASEOS\nEDIFICIO PÚBLICO")]
    [InlineData("{\\fArial|b1|i0|c0|p34;POZO BOMBEO}\\PT: +9.93 \\PP: 3.68", "POZO BOMBEO\nT: +9.93 \nP: 3.68")]
    [InlineData("\\A1;\\H0.25;CONDUCTO RESIDUAL %%C315 PVC", "CONDUCTO RESIDUAL Ø315 PVC")]
    [InlineData("\\S1/2;\"", "1/2\"")]
    [InlineData("\\S1#4;", "1/4")]
    [InlineData("m\\S^2;", "m2")]
    [InlineData("a\\\\b \\{x\\}", "a\\b {x}")]
    [InlineData("\\LSubrayado\\l y \\Otachado\\o", "Subrayado y tachado")]
    [InlineData("\\C1;rojo\\~texto", "rojo texto")]
    [InlineData("\\pxi-3,l3;lista", "lista")]
    [InlineData("1 ∅ 160", "1 ∅ 160")]
    public void PlainMText_removes_formatting(string contents, string expected)
        => Assert.Equal(expected, CadTextDecoder.PlainMText(contents));

    [Fact]
    public void Query_projection_reads_leader_text_and_plain_text_and_disposes_the_copy()
    {
        var mtext = new MText { Contents = "POZO BOMBEO\\PT: +9.93", Location = new Point3d(10, 20, 0) };
        var leader = new MLeader { MText = mtext };

        var row = CadEntityProjection.Project(leader, new[] { "handle", "text", "plain_text", "position" }, null, true, out _, out var failed, "2A");

        Assert.Equal(0, failed);
        Assert.Equal("POZO BOMBEO\\PT: +9.93", row.Value<string>("text"));
        Assert.Equal("POZO BOMBEO\nT: +9.93", row.Value<string>("plain_text"));
        Assert.Equal(10, row["position"]!.Value<double>("x"));
        Assert.Equal(20, row["position"]!.Value<double>("y"));
        Assert.Equal(3, mtext.TextReadDisposeCalls);
    }

    [Fact]
    public void Query_projection_keeps_raw_text_and_adds_plain_text_for_dbtext()
    {
        var text = new DBText { TextString = "1T %%C90 ALIVIADERO", Position = new Point3d(1, 2, 0) };

        var row = CadEntityProjection.Project(text, new[] { "handle", "text", "plain_text" }, null, true, out _, out _, "1A");

        Assert.Equal("1T %%C90 ALIVIADERO", row.Value<string>("text"));
        Assert.Equal("1T Ø90 ALIVIADERO", row.Value<string>("plain_text"));
    }

    [Fact]
    public void Query_projection_marks_plain_text_unsupported_for_geometry()
    {
        var row = CadEntityProjection.Project(new Line(), new[] { "handle", "plain_text" }, null, true, out _, out _, "3A");

        Assert.Contains("plain_text", row["unavailable_fields"]!.Values<string>());
        Assert.Equal("not_supported_for_type", row["field_unavailability"]!.Value<string>("plain_text"));
    }
}
