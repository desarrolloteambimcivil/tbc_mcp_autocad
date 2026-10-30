using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Bimwright.Dwg.Server
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            if (args != null && args.Any(a => a == "--help" || a == "-h"))
            {
                PrintHelp();
                return;
            }

            var config = DwgMcpConfig.Load(args);
            ValidateTarget(config.Target);
            ServerState.Config = config;
            Bimwright.Dwg.Plugin.DiagnosticLog.MaintainLegacyLogs();
            Bimwright.Dwg.Plugin.OutputArtifactStore.MaintainInBackground();
            WarnIfUnwiredOptions(config);
            var enabled = ToolsetFilter.Resolve(config);
            _ = GatewayRuntime.Identity; // Capture loaded gateway identity before initialize/tools/list.

            var builder = Host.CreateApplicationBuilder(args);
            // MCP stdio transport owns stdout. Route logs to stderr.
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
            var mcp = builder.Services
                .AddMcpServer(ConfigureMcpServerOptions)
                .WithStdioServerTransport();
            mcp = RegisterToolsets(mcp, enabled);
            mcp = ResponseBudget.Register(mcp, config);
            mcp = ToolArgumentGuard.Register(mcp);

            await builder.Build().RunAsync();
        }

        private static void ValidateTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            AuthToken.NormalizeTarget(target);
        }

        public static string UnwiredOptionWarning(DwgMcpConfig config)
        {
            if (config != null && config.AllowLanBindOrDefault)
            {
                return "warning: --allow-lan-bind / TBC_ACAD_MCP_ALLOW_LAN_BIND is parsed but plugin-side LAN binding is not yet implemented in v1.0; the AutoCAD plugin still listens on loopback only.";
            }

            return null;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(
                "tbc-acad-mcp - MCP stdio gateway for Autodesk AutoCAD and Civil 3D 2022-2027.\n" +
                "\n" +
                "Usage: tbc-acad-mcp [options]\n" +
                "\n" +
                "Options:\n" +
                "  --target <selector>    AutoCAD year (2022-2027), pid:<PID> or id:acad-<year>-<PID>.\n" +
                "                         Verify with dwg_switch_target before writes.\n" +
                "  --toolsets <csv>       Toolsets to register (default: query,modify,meta,view).\n" +
                "                         'all' enables the full surface.\n" +
                "  --read-only            Strip every write/execute tool, including dwg_switch_target.\n" +
                "                         Select reads at startup with --target pid:<PID> or id:<target_id>.\n" +
                "                         On target loss, list targets and restart with the intended selector.\n" +
                "  --enable-send-code / --disable-send-code (default: enabled).\n" +
                "  --enable-call-log / --disable-call-log (default: disabled).\n" +
                "  --enable-toolbaker / --disable-toolbaker\n" +
                "  --response-budget-bytes <n>       Final MCP tool-result budget (default: 83886080).\n" +
                "  --response-warn-bytes <n>         Warning threshold (default: 1048576, capped by budget).\n" +
                "  --response-strong-warn-bytes <n>  Strong warning threshold (default: 8388608, capped by budget).\n" +
                "                         ASCII decimal bytes; budget >= 4096 and 0 < warn <= strong <= budget.\n" +
                "                         Per tool result, excluding the JSON-RPC envelope; no automatic retry.\n" +
                "  --allow-lan-bind       Parsed but not yet implemented (loopback only).\n" +
                "  --log-level <level>    trace|debug|information|warning|error.\n" +
                "  --config <path>        JSON config file. Default:\n" +
                "                         %LOCALAPPDATA%\\NourRouas\\tbc-acad-mcp\\dwgmcp.config.json\n" +
                "\n" +
                "Environment overrides: TBC_ACAD_MCP_TARGET, TBC_ACAD_MCP_TOOLSETS,\n" +
                "TBC_ACAD_MCP_READ_ONLY, TBC_ACAD_MCP_ENABLE_TOOLBAKER,\n" +
                "TBC_ACAD_MCP_ENABLE_SEND_CODE, TBC_ACAD_MCP_ENABLE_CALL_LOG,\n" +
                "TBC_ACAD_MCP_ALLOW_LAN_BIND, TBC_ACAD_MCP_LOG_LEVEL,\n" +
                "TBC_ACAD_MCP_RESPONSE_BUDGET_BYTES, TBC_ACAD_MCP_RESPONSE_WARN_BYTES,\n" +
                "TBC_ACAD_MCP_RESPONSE_STRONG_WARN_BYTES.\n" +
                "The legacy BIMWRIGHT_DWG_* names are read when the TBC_ACAD_MCP_* name is not set.\n" +
                "\n" +
                "  -h, --help             Show this help and exit.");
        }

        private static void WarnIfUnwiredOptions(DwgMcpConfig config)
        {
            var warning = UnwiredOptionWarning(config);
            if (!string.IsNullOrEmpty(warning))
            {
                Console.Error.WriteLine(warning);
            }
        }
    }
}
