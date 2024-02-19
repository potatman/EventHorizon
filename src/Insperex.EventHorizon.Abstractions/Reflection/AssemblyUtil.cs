using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Insperex.EventHorizon.Abstractions.Reflection
{
    public static class AssemblyUtil
    {
        private static readonly Assembly Assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        public static readonly string AssemblyName = Assembly.GetName().Name;
        public static string AssemblyNameWithGuid => $"{AssemblyName}_{Guid.NewGuid().ToString()[..8]}";

        private static readonly Type[] AssemblyTypes = DependencyContext.Default?.CompileLibraries
            .SelectMany(x =>
            {
                try
                {
                    return Assembly.Load(x.Name)?.GetTypes();
                }
                catch (Exception)
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(x => x != null)
            .ToArray();

        public static Type[] GetTypes<T>() => AssemblyTypes
            .Where(x => typeof(T).IsAssignableFrom(x) && x.IsClass).ToArray();
    }
}
