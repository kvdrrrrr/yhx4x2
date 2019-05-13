using System.IO;
using System.Linq;
using System.Reflection;

namespace Yhx4x2.x64
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var attributes = typeof(Program).GetTypeInfo().Assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute));
            var assemblyTitleAttribute = attributes.SingleOrDefault() as AssemblyTitleAttribute;

            var titleSplit = assemblyTitleAttribute?.Title.Split('.');

            if (titleSplit != null)
            {
                var assembly = Assembly.LoadFile(Path.GetFullPath(titleSplit[0] + ".exe"));
                assembly.EntryPoint.Invoke(null, new object[] {args});
            }
        }
    }
}