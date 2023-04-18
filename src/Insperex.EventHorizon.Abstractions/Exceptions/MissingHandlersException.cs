using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Insperex.EventHorizon.Abstractions.Exceptions;

public class MissingHandlersException : Exception
{
    public MissingHandlersException(MemberInfo state, IEnumerable<Type> subStates, IEnumerable<Type> streams, string[] errors)
        : base( 
            $"State : {state.Name}{Environment.NewLine}" 
            + $"SubStates: {string.Join(",", subStates.Select(x => x.Name))}{Environment.NewLine}"
            + $"EventStreams: {string.Join(",", streams.Select(x => x.Name))}{Environment.NewLine}"
            + $"Missing Handlers: {Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", errors)}{Environment.NewLine}"
        ) { }
}

