using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.Abstractions.Reflection
{
    public static class ReflectionFactory
    {
        //TODO: Load All States, so that we can read all
        private static readonly Dictionary<Type, TypeDetail> TypeDict = new();
        private static readonly Dictionary<Type, StateDetail> StateDict = new();

        public static TypeDetail GetTypeDetail(Type type)
        {
            if (TypeDict.TryGetValue(type, out var detail))
                return detail;
            return TypeDict[type] = new TypeDetail(type);
        }

        public static StateDetail GetStateDetail(Type type)
        {
            if (StateDict.TryGetValue(type, out var detail))
                return detail;
            return StateDict[type] = new StateDetail(type);
        }
    }
}
