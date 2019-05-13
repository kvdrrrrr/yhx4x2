﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using CommandLine;
using CommandLine.Text;

namespace Yhx4x2
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            void Configuration(ParserSettings with)
            {
                with.EnableDashDash = true;
                with.CaseInsensitiveEnumValues = true;
                with.AutoVersion = false;
                with.AutoHelp = false;
                with.HelpWriter = null;
                with.IgnoreUnknownArguments = true;
            }

            var parser = new Parser(Configuration);

            var result = parser.ParseArguments<Options>(args);
            
            result
                .WithParsed(StartProcessing)
                .WithNotParsed(errors => {                        
                    var helpText = HelpText.AutoBuild(result, h => HelpText.DefaultParsingErrorsHandler(result, h), e => e);
                    helpText.AddEnumValuesToHelpText = true;
                    helpText.AddDashesToOption = true;
                    helpText.Heading = "Yhx4x2 Injector";
                    helpText.Copyright = "aka bleak wrapper by kvdr 2019";
                    Console.Error.WriteLine(helpText);
                    
                });
        }
        
        private static void StartProcessing(Options options)
        {
            Process targetProcess;
            
            // PID
            if (int.TryParse(options.TargetProcess, out var pid))
            {
                try
                {
                    targetProcess = Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    Console.Error.WriteLine($"Invalid target process {options.TargetProcess}: not running.");
                    return;
                }
            }
            // Process name
            else
            {
                var processName = options.TargetProcess;
                // Get rid of extension
                if (processName.Contains("."))
                    processName = processName.Split('.')[0];
                

                var processes = Process.GetProcessesByName(processName);

                if (processes.Length != 1)
                {
                    Console.Error.WriteLine($"Invalid target process {options.TargetProcess}: not running or multiple instances are running.");
                    return;
                }

                targetProcess = processes[0];
            }
            
            var dllsDataToInject = new List<string>();

            foreach (var dllFile in options.DllFiles)
            {
                var t = dllFile;
                
                string relFileName = null;
                if (dllFile.StartsWith("[["))
                {
                    var idx = dllFile.IndexOf("]]", 2, StringComparison.OrdinalIgnoreCase);
                    relFileName = dllFile.Substring(2, idx - 2);
                    t = dllFile.Substring(4 + relFileName.Length, dllFile.Length - 4 - relFileName.Length);
                    
                    Console.WriteLine($"Local temp file forced to: {relFileName}");
                }

                // URL, download & write to temp file
                if (Uri.TryCreate(t, UriKind.Absolute, out var uriResult) &&
                    (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    Console.WriteLine($"Downloading DLL from: {t}");
                    
                    Directory.CreateDirectory("temp");

                    var fileRequest = (HttpWebRequest)WebRequest.Create(uriResult);    
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
                    
                    var fileResponse = (HttpWebResponse)fileRequest.GetResponse();
                    
                    using (var rs = fileResponse.GetResponseStream())
                    using (var ms = new MemoryStream())
                    {
                        // read response into memory
                        rs.CopyTo(ms);
                        
                        ms.Position = 0;

                        // are we dealing with a gzip file?
                        var gzip = false;
                        var sr = new BinaryReader(ms);
                        var gzipHeader = new byte[] { 0x1F, 0x8B, 0x08 };
                        var header = sr.ReadBytes(3);

                        if (header.SequenceEqual(gzipHeader))
                        {
                            Console.WriteLine("Decompressing...");
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
                var injector = new Bleak.Injector(options.InjectionMethod, targetProcess.Id, dllData, options.RandomiseDllName);
                var baseAddr = injector.InjectDll();
                Console.WriteLine($"Injected {Path.GetFileName(dllData)} @ {baseAddr.ToInt64():X16}.");

                if (options.ScramblePE)
                {
                    switch (injector.RandomiseDllHeaders())
                    {
                        case true:
                            Console.WriteLine("Scrambled headers.");
                            break;
                        case false:
                            Console.Error.WriteLine("Failed to scramble headers.");
                            break;
                    }
                }

                if (options.HideFromPeb)
                {
                    switch (injector.HideDllFromPeb())
                    {
                        case true:
                            Console.WriteLine("Hidden from peb.");
                            break;
                        case false:
                            Console.Error.WriteLine("Failed to hide from peb.");
                            break;
                    }
                }
            }
        }
    }
}