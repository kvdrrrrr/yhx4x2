using System;
using Microsoft.Win32;

namespace Installer
{
    internal static class Yhx4Protocol
    {
        private const string Protocol = "yhx4";
        private const string ProtocolHandler = "url.yhx4";
        private const string CompanyName = "kvdr";
        private const string ProductName = "yhx4x2";

        private static string Launch(string path) => $"\"{path}\" \"%1\"";

        private static readonly Version Win8Version = new Version(6, 2, 9200, 0);

        private static readonly bool IsWin8 =
            Environment.OSVersion.Platform == PlatformID.Win32NT &&
            Environment.OSVersion.Version >= Win8Version;

        public static void Register(string path)
        {
            if (IsWin8)
                RegisterWin8(path);
            else
                RegisterWin7(path);
        }

        private static void RegisterWin7(string path)
        {
            var regKey = Registry.ClassesRoot.CreateSubKey(Protocol);

            if (regKey != null)
            {
                regKey.SetValue(null, "URL:yhx4 Protocol");
                regKey.SetValue("URL Protocol", "");

                regKey = regKey.CreateSubKey(@"shell\open\command");
                regKey.SetValue(null, Launch(path));
            }
        }

        private static void RegisterWin8(string path)
        {
            RegisterWin7(path);

            var regKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Classes")?.CreateSubKey(ProtocolHandler);

            regKey?.SetValue(null, Protocol);

            regKey?.CreateSubKey(@"shell\open\command")?.SetValue(null, Launch(path));

            Registry.LocalMachine
                .CreateSubKey(
                    $@"SOFTWARE\{CompanyName}\{ProductName}\Capabilities\ApplicationDescription\URLAssociations")
                ?.SetValue(Protocol, ProtocolHandler);

            Registry.LocalMachine.CreateSubKey(@"SOFTWARE\RegisteredApplications")
                ?.SetValue(ProductName, $@"SOFTWARE\{ProductName}\Capabilities");
        }

        public static void Unregister()
        {
            if (!IsWin8)
            {
                Registry.ClassesRoot.DeleteSubKeyTree("yhx4", false);
                return;
            }

            // Extra work required
            Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Classes")?.DeleteSubKeyTree(ProtocolHandler, false);

            Registry.LocalMachine.DeleteSubKeyTree($@"SOFTWARE\{CompanyName}\{ProductName}");

            Registry.LocalMachine.CreateSubKey(@"SOFTWARE\RegisteredApplications")?.DeleteValue(ProductName);
        }
    }
}