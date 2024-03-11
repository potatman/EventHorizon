using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;

namespace Insperex.EventHorizon.Abstractions.Reflection
{
    public class TypeDetail
    {
        public Type Type { get; set; }
        public Assembly Assembly { get; set; }
        public string AssemblyName { get; set; }
        public Type[] AssemblyTypes { get; set; }

        public TypeDetail(Type type)
        {
            Type = type;
            Assembly = type.Assembly;
            AssemblyName = Assembly.GetName().Name;
            AssemblyTypes = Assembly.GetTypes();
        }

        public Dictionary<string, Type> GetTypes<T>() => AssemblyTypes
            .Where(x => typeof(T).IsAssignableFrom(x) && x.IsClass)
            .ToDictionary(x => x.Name);
    }
}
