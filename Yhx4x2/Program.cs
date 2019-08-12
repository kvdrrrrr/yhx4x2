using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;
using CommandLine.Text;

namespace Yhx4x2
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var launchedByProtocol = false;
            
            List<string> newArgs;
            if (args.Length == 1 && args[0].StartsWith("yhx4://"))
            {
                newArgs = new List<string> {"inject"};

                var decoded = WebUtility.UrlDecode(args[0].Substring(7)).Split(' ');

                newArgs.AddRange(decoded);

                launchedByProtocol = true;
            }
            else
            {
                newArgs = new List<string>(args);
            }
            
            void Configuration(ParserSettings with)
            {
                with.EnableDashDash = true;
                with.CaseInsensitiveEnumValues = true;
                with.AutoVersion = false;
                with.AutoHelp = false;
                with.HelpWriter = null;
                with.IgnoreUnknownArguments = true;
            }

            void ParseErrors<T>(ParserResult<T> resultArg)
            {
                var helpText = HelpText.AutoBuild(resultArg, h => HelpText.DefaultParsingErrorsHandler(resultArg, h),
                    e => e);
                helpText.AddEnumValuesToHelpText = true;
                helpText.AddDashesToOption = true;
                helpText.Heading = "Yhx4x2 Injector";
                helpText.Copyright = "aka bleak wrapper by kvdr 2019";
                helpText.AutoVersion = false;
                helpText.AddOptions(resultArg);
                Console.Error.WriteLine(helpText);
            }

            var parser = new Parser(Configuration);

            var result = parser.ParseArguments<InjectOptions>(newArgs);

            result
                .WithParsed(options => PerformEscalatedAction(() => StartProcessing(options), newArgs))
                .WithNotParsed(x => ParseErrors(result));


            if (launchedByProtocol)
            {
                Console.WriteLine("Done, exiting in 5 seconds.");
                Thread.Sleep(5000);
            }
        }

        private static void StartProcessing(InjectOptions injectOptions)
        {
            Process targetProcess;

            // PID
            if (int.TryParse(injectOptions.TargetProcess, out var pid))
            {
                try
                {
                    targetProcess = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    Console.Error.WriteLine($"Invalid target process {injectOptions.TargetProcess}: not running.");
                    return;
                }
            }
            // Process name
            else
            {
                var processName = injectOptions.TargetProcess;
                // Get rid of extension
                if (processName.Contains("."))
                    processName = processName.Split('.')[0];


                var processes = Process.GetProcessesByName(processName);

                if (processes.Length != 1)
                {
                    Console.Error.WriteLine(
                        $"Invalid target process {injectOptions.TargetProcess}: not running or multiple instances are running.");
                    return;
                }

                targetProcess = processes[0];
            }

            var dllsDataToInject = new List<string>();

            foreach (var dllFile in injectOptions.DllFiles)
            {
                var t = dllFile;

                string relFileName = null;
                if (dllFile.StartsWith("[["))
                {
                    var idx = dllFile.IndexOf("]]", 2, StringComparison.OrdinalIgnoreCase);
                    relFileName = dllFile.Substring(2, idx - 2);
                    t = dllFile.Substring(4 + relFileName.Length);
                }

                // URL, download & write to temp file
                if (Uri.TryCreate(t, UriKind.Absolute, out var uriResult) &&
                    (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    Console.WriteLine($"Downloading DLL from: {t}");

                    var result = Directory.CreateDirectory("temp");

                    if (!result.Exists)
                    {
                        Console.WriteLine("Failed to write to temporary directory.");
                        return;
                    }
                    
                    var fileRequest = (HttpWebRequest) WebRequest.Create(uriResult);
                    fileRequest.MaximumAutomaticRedirections = 3;
                    fileRequest.AllowAutoRedirect = true;
                    fileRequest.CookieContainer = new CookieContainer();
                    fileRequest.Method = "GET";

                    var split = uriResult.UserInfo.Split(':');
                    if (split.Length > 1)
                    {
                        Console.WriteLine($"Using credentials: {uriResult.UserInfo}");

                        var credCache = new CredentialCache
                        {
                            {uriResult, "Basic", new NetworkCredential(split[0], split[1])}
                        };

                        fileRequest.Credentials = credCache;
                        fileRequest.PreAuthenticate = true;
                    }

                    var fileResponse = (HttpWebResponse) fileRequest.GetResponse();

                    using (var rs = fileResponse.GetResponseStream())
                    using (var ms = new MemoryStream())
                    {
                        // read response into memory
                        rs.CopyTo(ms);

                        ms.Position = 0;

                        // are we dealing with a gzip file?
                        var gzip = false;
                        var zip = false;
                        var sr = new BinaryReader(ms);
                        var gzipHeader = new byte[] {0x1F, 0x8B, 0x08};
                        var zipHeader = new byte[] {0x50, 0x4B, 0x03, 0x04};
                        var header = sr.ReadBytes(4);

                        if (header.SequenceEqual(zipHeader))
                        {
                            Console.WriteLine("Decompressing ZIP...");
                            zip = true;
                        }
                        else if (header.Take(3).SequenceEqual(gzipHeader))
                        {
                            Console.WriteLine("Decompressing GZIP...");
                            gzip = true;
                        }

                        ms.Position = 0;

                        MemoryStream targetStream;
                        if (gzip)
                        {
                            targetStream = new MemoryStream();

                            var gZipStream = new GZipStream(ms, CompressionMode.Decompress);
                            gZipStream.CopyTo(targetStream);

                            targetStream.Position = 0;
                        }
                        else if (zip)
                        {
                            var zipArchive = new ZipArchive(ms, ZipArchiveMode.Read);

                            var count = 0;
                            
                            foreach (var entry in zipArchive.Entries)
                            {
                                if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    var fileName1 = Path.GetFullPath(Path.Combine("temp", entry.Name));

                                    using (var fs = File.OpenWrite(fileName1))
                                    {
                                        entry.Open().CopyTo(fs);
                                    }

                                    dllsDataToInject.Add(fileName1);
                                    
                                    count++;
                                }
                            }

                            Console.WriteLine($"Decompressed {count} dll files.");
                            
                            return;
                        }
                        else
                        {
                            targetStream = ms;
                        }

                        if (relFileName == null)
                        {
                            var crc32 = new Crc32();
                            relFileName = BitConverter.ToString(crc32.ComputeHash(targetStream)).Replace("-", "");
                            relFileName += ".dll";
                        }

                        Console.WriteLine($"Writing temp file to: {relFileName}");
                        
                        targetStream.Position = 0;

                        var fileName = Path.GetFullPath(Path.Combine("temp", relFileName));

                        using (var fs = File.OpenWrite(fileName))
                        {
                            targetStream.CopyTo(fs);
                        }

                        dllsDataToInject.Add(fileName);

                        targetStream.Flush();
                        targetStream.Dispose();
                    }
                }
                // Local file
                else
                {
                    var fullPath = Path.GetFullPath(dllFile);

                    if (!File.Exists(fullPath))
                    {
                        Console.Error.WriteLine($"Specified DLL file does not exist: {dllFile}.");
                        return;
                    }

                    dllsDataToInject.Add(fullPath);
                }
            }

            // Inject stuff
            foreach (var dllData in dllsDataToInject)
            {
                //injectOptions.InjectionMethod, targetProcess.Id, dllData, injectOptions.RandomiseDllName

                var injflags = Bleak.InjectionFlags.None;

                if (injectOptions.ScramblePE)
                {
                    injflags |= Bleak.InjectionFlags.RandomiseDllHeaders;
                }

                if (injectOptions.HideFromPeb)
                {
                    injflags |= Bleak.InjectionFlags.HideDllFromPeb;
                }

                if (injectOptions.RandomiseDllName)
                {
                    injflags |= Bleak.InjectionFlags.RandomiseDllName;
                }

                var injector = new Bleak.Injector(targetProcess.Id, dllData, injectOptions.InjectionMethod, injflags);

                var baseAddr = injector.InjectDll();
                Console.WriteLine($"Injected {Path.GetFileName(dllData)} @ {baseAddr.ToInt64():X16}.");
            }
        }

        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsUserAnAdmin();

        private static void PerformEscalatedAction(Action action, List<string> newArgs)
        {
            if (IsUserAnAdmin())
            {
                action();
            }
            else
            {
                var startInfo = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe"))
                {
                    Verb = "runas", 
                    Arguments = string.Join(" ", newArgs),
                    UseShellExecute = true
                };

                var process = new Process
                {
                    StartInfo = startInfo
                };

                process.Start();
                process.WaitForExit();
            }
        }
    }
}