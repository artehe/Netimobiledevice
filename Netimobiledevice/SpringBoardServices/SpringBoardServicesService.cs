using Netimobiledevice.Lockdown;
using Netimobiledevice.Lockdown.Services;
using Netimobiledevice.Plist;
using System;

namespace Netimobiledevice.SpringBoardServices
{
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

        /// <summary>
        /// Get the icon of the application with the specified <paramref name="bundleId"/>.
        /// </summary>
        /// <param name="bundleId">The bundle identifier of the applicaition.</param>
        /// <returns>The byte array containing the PNG icon.</returns>
        public DataNode GetIconPNGData(string bundleId)
        {
            DictionaryNode command = CreateCommand("getIconPNGData");
            command.Add("bundleId", new StringNode(bundleId));
            return ExecuteCommand(command, "pngData").AsDataNode();
        }

        /// <summary>
        /// Get the orientation of the device's screen.
        /// </summary>
        public ScreenOrientation GetScreenOrientation()
        {
            DictionaryNode command = CreateCommand("getInterfaceOrientation");
            return (ScreenOrientation) ExecuteCommand(command, "interfaceOrientation").AsIntegerNode().Value;
        }

        /// <summary>
        /// Get the wallpaper image of the device.
        /// </summary>
        /// <returns>The byte array containing the PNG wallpaper.</returns>
        public DataNode GetWallpaperPNGData()
        {
            DictionaryNode command = CreateCommand("getHomeScreenWallpaperPNGData");
            return ExecuteCommand(command, "pngData").AsDataNode();
        }

        /// <summary>
        /// Sets the icon state of the connected device.
        /// </summary>
        /// <param name="newState">A plist containing the new iconstate.</param>
        public DictionaryNode SetIconState(DictionaryNode? newState = null)
        {
            DictionaryNode command = CreateCommand("setIconState");
            newState ??= new DictionaryNode();
            command.Add("iconState", newState);
            return Service.SendReceivePlist(command)?.AsDictionaryNode() ?? new DictionaryNode();
        }
    }
}
