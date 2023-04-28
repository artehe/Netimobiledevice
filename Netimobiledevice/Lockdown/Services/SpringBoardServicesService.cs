using Netimobiledevice.Plist;
using System;

namespace Netimobiledevice.Lockdown.Services
{
    public enum ScreenOrientation
    {
        Portrait = 1,
        PortraitUpsideDown = 2,
        Landscape = 3,
        LandscapeHomeToLeft = 4
    }

    public sealed class SpringBoardServicesService : BaseService
    {
        protected override string ServiceName => "com.apple.springboardservices";

        public SpringBoardServicesService(LockdownClient lockdownClient) : base(lockdownClient) { }

        private static DictionaryNode CreateCommand(string command)
        {
            DictionaryNode cmd = new DictionaryNode() {
                { "command", new StringNode(command) },
            };
            return cmd;
        }

        private PropertyNode ExecuteCommand(DictionaryNode command, string responseNode)
        {
            DictionaryNode response = Service.SendReceivePlist(command)?.AsDictionaryNode() ?? new DictionaryNode();
            if (response.ContainsKey(responseNode)) {
                return response[responseNode];
            }
            throw new Exception($"Node {responseNode} doesn't exist in response");
        }

        public DataNode GetIconPNGData(string bundleId)
        {
            DictionaryNode command = CreateCommand("getIconPNGData");
            command.Add("bundleId", new StringNode(bundleId));
            return ExecuteCommand(command, "pngData").AsDataNode();
        }

        public DictionaryNode GetIconState(string formatVersion = "2")
        {
            DictionaryNode command = CreateCommand("getIconState");
            if (!string.IsNullOrWhiteSpace(formatVersion)) {
                command.Add("formatVersion", new StringNode(formatVersion));
            }
            return Service.SendReceivePlist(command)?.AsDictionaryNode() ?? new DictionaryNode();
        }

        public ScreenOrientation GetScreenOrientation()
        {
            DictionaryNode command = CreateCommand("getInterfaceOrientation");
            return (ScreenOrientation) ExecuteCommand(command, "interfaceOrientation").AsIntegerNode().Value;
        }

        public DataNode GetWallpaperPNGData()
        {
            DictionaryNode command = CreateCommand("getHomeScreenWallpaperPNGData");
            return ExecuteCommand(command, "pngData").AsDataNode();
        }

        public DictionaryNode SetIconState(DictionaryNode? newState = null)
        {
            DictionaryNode command = CreateCommand("setIconState");
            newState ??= new DictionaryNode();
            command.Add("iconState", newState);
            return Service.SendReceivePlist(command)?.AsDictionaryNode() ?? new DictionaryNode();
        }
    }
}
