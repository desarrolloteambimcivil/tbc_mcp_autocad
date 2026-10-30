#nullable enable
using System.Collections.Generic;
using System.IO;
using Bimwright.Dwg.Plugin;
using Bimwright.Dwg.Server;
using Xunit;

namespace Bimwright.Dwg.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void Data_root_is_the_tbc_acad_mcp_folder_under_the_vendor_folder()
    {
        var local = Path.Combine("C:", "Users", "someone", "AppData", "Local");
        Assert.Equal(Path.Combine(local, "NourRouas"), ProductIdentity.VendorRoot(local));
        Assert.Equal(Path.Combine(local, "NourRouas", "tbc-acad-mcp"), ProductIdentity.DataRoot(local));
    }

    [Fact]
    public void Current_env_name_wins_over_the_legacy_name()
    {
        var env = new Dictionary<string, string?> { ["TBC_ACAD_MCP_TARGET"] = "2025", ["BIMWRIGHT_DWG_TARGET"] = "2024" };
        Assert.Equal("2025", ProductIdentity.GetEnv("TBC_ACAD_MCP_TARGET", name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void Legacy_env_name_is_read_when_the_current_name_is_not_set()
    {
        var env = new Dictionary<string, string?> { ["BIMWRIGHT_DWG_TARGET"] = "2024" };
        Assert.Equal("2024", ProductIdentity.GetEnv("TBC_ACAD_MCP_TARGET", name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void Names_without_the_current_prefix_are_read_as_given()
    {
        var env = new Dictionary<string, string?> { ["BIMWRIGHT_DWG_TARGET"] = "2024" };
        Assert.Null(ProductIdentity.GetEnv("OTHER_TARGET", name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void Gateway_config_reads_the_legacy_env_name()
    {
        var env = new Dictionary<string, string?> { ["BIMWRIGHT_DWG_TARGET"] = "pid:42" };
        var config = DwgMcpConfig.Load(new string[0], Path.Combine(Path.GetTempPath(), "missing-" + System.Guid.NewGuid().ToString("N") + ".json"), name => env.GetValueOrDefault(name));
        Assert.Equal("pid:42", config.Target);
    }
}
