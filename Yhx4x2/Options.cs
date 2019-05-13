using System.Collections.Generic;
using Bleak;
using CommandLine;

namespace Yhx4x2
{
    public class Options
    {
        public Options(IEnumerable<string> dllFiles, string targetProcess, InjectionMethod injectionMethod, bool randomiseDllName, bool scramblePe, bool hideFromPeb)
        {
            DllFiles = dllFiles;
            TargetProcess = targetProcess;
            InjectionMethod = injectionMethod;
            RandomiseDllName = randomiseDllName;
            ScramblePE = scramblePe;
            HideFromPeb = hideFromPeb;
        }

        [Option(shortName: 'f', longName: "dll", Required = true, HelpText = "List of DLL files to inject. If an url is specified, it will be downloaded to a temporary location.")]
        public IEnumerable<string> DllFiles { get; }
        
        [Option(shortName: 'p', longName: "process", Required = true, HelpText = "Either a PID or name of the target process.")]
        public string TargetProcess { get; }
        
        [Option(shortName: 'm', longName: "method", Required = false, HelpText = "Injection method. Possible values: CreateRemoteThread, ManualMap, ThreadHijack")]
        public InjectionMethod InjectionMethod { get; }
        
        [Option(longName: "scrambleDllName", Required = false, HelpText = "Scramble DLL name.")]
        public bool RandomiseDllName { get; }
        
        [Option(longName: "scramblePE", Required = false, HelpText = "Scramble PE header of injected DLL.")]
        public bool ScramblePE { get; }
        
        [Option(longName: "hidePEB", Required = false, HelpText = "Hide injected DLL from PEB.")]
        public bool HideFromPeb { get; }
    }
}