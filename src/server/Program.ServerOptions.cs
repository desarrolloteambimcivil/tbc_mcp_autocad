using System.Linq;

namespace Bimwright.Dwg.Server
{
    public partial class Program
    {
        internal static readonly string ServerInformationalVersion = typeof(Program).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "0.0.0";
        internal static readonly string ServerVersion = ServerInformationalVersion.Split('+')[0];

        private static void ConfigureMcpServerOptions(ModelContextProtocol.Server.McpServerOptions opts)
        {
            opts.ServerInfo = new ModelContextProtocol.Protocol.Implementation
            {
                Name = Bimwright.Dwg.Plugin.ProductIdentity.Name,
                Title = "TBC AutoCAD MCP",
                Version = ServerInformationalVersion,
                Description = "Model Context Protocol gateway for Autodesk AutoCAD and Civil 3D DWG workflows",
                WebsiteUrl = Bimwright.Dwg.Plugin.ProductIdentity.RepositoryUrl
            };
            opts.ServerInstructions = ServerInstructions.Text;
        }
    }
}
