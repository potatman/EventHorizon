using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.Abstractions.Reflection
{
    public class StateDetail : TypeDetail
    {
        // Actions
        public readonly Dictionary<string, Type> CommandDict;
        public readonly Dictionary<string, Type> EventDict;
        public readonly Dictionary<string, Type> RequestDict;
        public readonly Dictionary<string, Type> ResponseDict;
        public readonly Dictionary<string, Type> ActionDict;

        // Handlers
        public readonly Dictionary<string, Dictionary<string, MethodInfo>> CommandHandlersDict;
        public readonly Dictionary<string, Dictionary<string, MethodInfo>> RequestHandlersDict;
        public readonly Dictionary<string, Dictionary<string, MethodInfo>> EventAppliersDict;


        public readonly PropertyInfo[] PropertiesWithStates;
        public readonly Type[] SubStates;
        public readonly Type[] AllStateTypes;

        public StateDetail(Type type) : base(type)
        {
            // State Properties
            PropertiesWithStates = type.GetProperties().Where(p => p.PropertyType.GetInterface(nameof(IState)) != null).ToArray();
            SubStates = PropertiesWithStates.Select(s => s.PropertyType).ToArray();
            AllStateTypes = SubStates.Concat([Type]).ToArray();
            AllStateTypes = AllStateTypes.Concat(GetSubTypes(AllStateTypes)).ToArray();

            // Actions
            EventDict = GetTypeDictWithGenericArg<IEvent>();
            CommandDict = GetTypeDictWithGenericArg<ICommand>();
            RequestDict = GetTypeDictWithGenericArg<IRequest>();
            ResponseDict = GetTypeDictWithGenericArg<IResponse>();
            ActionDict = GetTypeDictWithGenericArg<IAction>();

            // Handlers
            CommandHandlersDict = GetHandlers(typeof(IHandleCommand<>), "Handle");
            RequestHandlersDict = GetHandlers(typeof(IHandleRequest<,>), "Handle");
            EventAppliersDict = GetHandlers(typeof(IApplyEvent<>), "Apply");
        }

        public Dictionary<string, Type> GetTypeDictWithGenericArg<T>() => AllStateTypes.SelectMany(x => x.Assembly
                .GetTypes().Where(t =>
                {
                    return t.GetInterfaces().Any(i =>
                        typeof(T).IsAssignableFrom(i)
                        && i.GetGenericArguments().Any()
                        && i.GetGenericArguments()[0] == x);
                })
            )
            .ToDictionary(x => x.Name);

        private Dictionary<string, Dictionary<string, MethodInfo>> GetHandlers(MemberInfo type, string methodName)
        {
            return AllStateTypes.ToDictionary(x => x.Name, t => t.GetInterfaces()
                    .Where(i => i.Name == type.Name)
                    .ToDictionary(d => d.GetGenericArguments()[0].Name, d => d.GetMethod(methodName)))
                ;
        }

        private static Type[] GetSubTypes(Type[] types)
        {
            var newType = new List<Type>();
            foreach (var type in types)
            {
                var streamAttributes = new AttributeUtil().GetAll<StreamAttribute>(type).ToArray();
                var streamSubTypes = streamAttributes.Select(x => x.SubType).Where(x => x != null).ToArray();
                newType.AddRange(streamSubTypes);
            }

            return newType.ToArray();
        }

        public string[] Validate<TMessage>(Type typeHandler, string methodName)
        {
            var messages = GetTypeDictWithGenericArg<TMessage>();
            var handlers = GetHandlers(typeHandler, methodName);
            var missing = new List<string>();
            foreach (var message in messages)
            {
                var handler = handlers.Values.FirstOrDefault(h => h.Any(x => x.Key == message.Key));
                if (handler != null) continue;
                var argCount = typeHandler.GetGenericArguments().Length;
                var commas = argCount == 1? string.Empty : string.Join(",", Enumerable.Range(0, argCount));
                missing.Add(typeHandler.Name + "<" + message + commas + ">");
            }

            return missing.ToArray();
        }

    }
}
