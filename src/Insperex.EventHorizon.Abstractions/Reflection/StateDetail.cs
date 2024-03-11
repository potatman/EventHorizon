using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.Abstractions.Reflection
{
    public class StateDetail : TypeDetail
    {
        // Handlers
        public Dictionary<Type, Dictionary<string, MethodInfo>> HandlersDict { get; set; }
        public Dictionary<Type, Dictionary<string, Type>> MessageTypeDict { get; set; }

        public readonly PropertyInfo[] PropertiesWithStates;
        public readonly Type[] SubStates;
        public readonly StateDetail[] SubStateDetails;
        public readonly Type[] AllStateTypes;

        public StateDetail(Type type) : base(type)
        {
            // State Properties
            PropertiesWithStates = type.GetProperties().Where(p => p.PropertyType.GetInterface(nameof(IState)) != null).ToArray();
            SubStates = PropertiesWithStates.Select(s => s.PropertyType).ToArray();
            SubStateDetails = SubStates.Select(x => new StateDetail(x)).ToArray();
            AllStateTypes = SubStates.Concat([Type]).ToArray();

            // Handlers
            HandlersDict = new Dictionary<Type, Dictionary<string, MethodInfo>>
            {
                [typeof(Command)] = GetHandlers(typeof(IHandleCommand<>), "Handle"),
                [typeof(Request)] = GetHandlers(typeof(IHandleRequest<,>), "Handle"),
                [typeof(Event)] = GetHandlers(typeof(IApplyEvent<>), "Apply")
            };

            // Handler Types
            MessageTypeDict = new Dictionary<Type, Dictionary<string, Type>>
            {
                [typeof(Command)] = GetHandlerTypeDict(typeof(IHandleCommand<>)),
                [typeof(Request)] = GetHandlerTypeDict(typeof(IHandleRequest<,>)),
                [typeof(Event)] = GetHandlerTypeDict(typeof(IApplyEvent<>)),
                [typeof(Response)] = GetTypeDictWithGenericArg<IResponse>()
            };
        }

        public string[] Validate<TMessage>(Type typeHandler, string methodName)
        {
            var messages = GetTypeDictWithGenericArg<TMessage>();
            var handlers = GetHandlers(typeHandler, methodName);
            var missing = new List<string>();
            foreach (var message in messages)
            {
                var handler = handlers.GetValueOrDefault(message.Key);
                if (handler != null) continue;
                var argCount = typeHandler.GetGenericArguments().Length;
                var commas = argCount == 1? string.Empty : string.Join(",", Enumerable.Range(0, argCount));
                missing.Add(typeHandler.Name + "<" + message + commas + ">");
            }

            return missing.ToArray();
        }

        public object TriggerHandler<TMessage>(Dictionary<Type, object> stateDict, AggregateContext context, TMessage message)
            where TMessage : ITopicMessage
        {
            var messageType = typeof(TMessage);
            foreach (var state in stateDict)
            {
                var stateDetail = ReflectionFactory.GetStateDetail(state.Key);
                var method = stateDetail.HandlersDict[messageType].GetValueOrDefault(message.Type);
                if (method == null) continue;
                var payload = message.GetPayload(stateDetail.MessageTypeDict[messageType]);
                var result = method?.Invoke(state.Value, parameters: [payload, context]);
                return result;
            }

            return null;
        }

        public void TriggerApply(Dictionary<Type, object> stateDict, Event message)
        {
            var messageType = typeof(Event);
            foreach (var state in stateDict)
            {
                var stateDetail = ReflectionFactory.GetStateDetail(state.Key);
                var method = stateDetail.HandlersDict[messageType].GetValueOrDefault(message.Type);
                if(method == null) continue;
                var payload = message.GetPayload(stateDetail.MessageTypeDict[messageType]);
                method?.Invoke(state.Value, parameters: new [] { payload } );
            }
        }

        private Dictionary<string, MethodInfo> GetHandlers(MemberInfo handlerType, string methodName)
        {
            return Type.GetInterfaces()
                .Where(i => i.Name == handlerType.Name)
                .ToDictionary(d => d.GetGenericArguments()[0].Name, d => d.GetMethod(methodName));
        }

        private Dictionary<string, Type> GetHandlerTypeDict(MemberInfo handlerType)
        {
            return Type.GetInterfaces()
                .Where(i => i.Name == handlerType.Name)
                .Select(d => d.GetGenericArguments()[0])
                .ToDictionary(d => d.Name);
        }

        private Dictionary<string, Type> GetTypeDictWithGenericArg<T>()
        {
            var type = typeof(T);
            return Type.Assembly
                .GetTypes().Where(t =>
                {
                    return t.GetInterfaces().Any(i =>
                        type.IsAssignableFrom(i)
                        && i.GetGenericArguments().Any()
                        && i.GetGenericArguments()[0] == Type);
                })
                .ToDictionary(x => x.Name);
        }
    }
}
