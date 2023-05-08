using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing.Extensions
{
    public static class WebApplicationExtensions
    {
        public static void MapEventSourcingEndpoints<T>(this WebApplication app) where T : class, IState, new()
        {
            var type = typeof(T);

            // Map Get
            var esClient = app.Services.GetRequiredService<EventSourcingClient<T>>();
            var aggregator = esClient.Aggregator().Build();
            app.MapGet(type.Name + "/{id}", async (string id) =>
                {
                    var response = await aggregator.GetAggregateFromStateAsync(id, CancellationToken.None);
                    return response.Exists() ? Results.Ok(response.State) : Results.NotFound();
                })
                .WithTags(type.Name);
            app.MapGet(type.Name + "/{id}/state-in-time", async (string id, [FromQuery] DateTime dateTime) =>
                {
                    var response = await aggregator.GetAggregateFromStateAsync(id, CancellationToken.None);
                    return response.Exists() ? Results.Ok(response.State) : Results.NotFound();
                })
                .WithTags(type.Name);

            // Map Requests
            var requests = AssemblyUtil.ActionDict.Values
                .Where(x => x.GetInterfaces().Any(i => i.Name == typeof(IRequest<,>).Name &&  i.GetGenericArguments()[0] == type))
                .ToDictionary(x => x, x => x.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IRequest<,>).Name));
            var methodReq = typeof(WebApplicationExtensions).GetMethod("MapRequest", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var request in requests)
            {
                var requestArgs = request.Value.GetGenericArguments();
                var generic = methodReq?.MakeGenericMethod(request.Key, requestArgs[1], requestArgs[0]);
                generic?.Invoke(obj: null, parameters: new object[] { app });
            }

            // Map Commands
            var commands = AssemblyUtil.ActionDict.Values
                .Where(x => x.GetInterfaces().Any(i => i.Name == typeof(ICommand<>).Name &&  i.GetGenericArguments()[0] == type))
                .ToDictionary(x => x, x => x.GetInterfaces().FirstOrDefault(i => i.Name == typeof(ICommand<>).Name));
            var methodCmd = typeof(WebApplicationExtensions).GetMethod("MapCommand", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var command in commands)
            {
                var requestArgs = command.Value.GetGenericArguments();
                var generic = methodCmd?.MakeGenericMethod(command.Key, requestArgs[0]);
                generic?.Invoke(obj: null, parameters: new object[] { app });
            }
        }

        private static void MapRequest<TReq, TRes, T>(this WebApplication app)
            where T : class, IState, new()
            where TReq : IRequest<T, TRes>
            where TRes : class, IResponse<T>
        {
            var esClient = app.Services.GetRequiredService<EventSourcingClient<T>>();
            var sender = esClient.CreateSender().Build();
            var typeName = typeof(T).Name;
            var reqName = typeof(TReq).Name;
            app.MapPost(typeName + "/{id}/" + reqName, async (string id, TReq req)  =>
                {
                    var response = await sender.SendAndReceiveAsync(id, req);
                    return Results.Ok(response);
                })
                .WithTags(typeName);
        }

        private static void MapCommand<TCmd, T>(this WebApplication app)
            where T : class, IState, new()
            where TCmd : ICommand<T>
        {
            var sender = app.Services.GetRequiredService<EventSourcingClient<T>>().CreateSender().Build();

            var typeName = typeof(T).Name;
            var reqName = typeof(TCmd).Name;
            app.MapPost(typeName + "/{id}/" + reqName, async (string id, TCmd cmd)  =>
                {
                    try
                    {
                        await sender.SendAsync(id, cmd);
                        return Results.Ok();
                    }
                    catch (Exception e)
                    {
                        return Results.Conflict(e);
                    }
                })
                .WithTags(typeName);
        }
    }
}
