using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing.Extensions
{
    /// <summary>
    /// Api Routing for Generic Hosting Application
    /// </summary>
    public static class EndpointRouterExtensions
    {
        public static void MapEventSourcingEndpoints<T>(this IEndpointRouteBuilder router) where T : class, IState, new()
        {
            var type = typeof(T);

            // Map Get
            var esClient = router.ServiceProvider.GetRequiredService<EventSourcingClient<T>>();
            var aggregator = esClient.Aggregator().Build();
            var group = router.MapGroup("api");

            group.MapGet(type.Name + "/{id}", async (string id) =>
                {
                    var response = await aggregator.GetAggregateFromStateAsync(id, CancellationToken.None);
                    return response.Exists() ? Results.Ok(response.State) : Results.NotFound();
                })
                .WithTags(type.Name);
            group.MapGet(type.Name + "/{id}/state-in-time", async (string id, [FromQuery] DateTime dateTime) =>
                {
                    var response = await aggregator.GetAggregateFromStateAsync(id, CancellationToken.None);
                    return response.Exists() ? Results.Ok(response.State) : Results.NotFound();
                })
                .WithTags(type.Name);

            // Map Requests
            var requests = AssemblyUtil.ActionDict.Values
                .Where(x => x.GetInterfaces().Any(i => i.Name == typeof(IRequest<,>).Name &&  i.GetGenericArguments()[0] == type))
                .ToDictionary(x => x, x => x.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IRequest<,>).Name));
            var methodReq = typeof(EndpointRouterExtensions).GetMethod("MapRequest", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var request in requests)
            {
                var requestArgs = request.Value.GetGenericArguments();
                var generic = methodReq?.MakeGenericMethod(request.Key, requestArgs[1], requestArgs[0]);
                generic?.Invoke(obj: null, parameters: new object[] { router });
            }

            // Map Commands
            var commands = AssemblyUtil.ActionDict.Values
                .Where(x => x.GetInterfaces().Any(i => i.Name == typeof(ICommand<>).Name &&  i.GetGenericArguments()[0] == type))
                .ToDictionary(x => x, x => x.GetInterfaces().FirstOrDefault(i => i.Name == typeof(ICommand<>).Name));
            var methodCmd = typeof(EndpointRouterExtensions).GetMethod("MapCommand", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var command in commands)
            {
                var requestArgs = command.Value.GetGenericArguments();
                var generic = methodCmd?.MakeGenericMethod(command.Key, requestArgs[0]);
                generic?.Invoke(obj: null, parameters: new object[] { router });
            }
        }

        private static void MapRequest<TReq, TRes, T>(this IEndpointRouteBuilder router)
            where T : class, IState, new()
            where TReq : IRequest<T, TRes>
            where TRes : class, IResponse<T>
        {
            var esClient = router.ServiceProvider.GetRequiredService<EventSourcingClient<T>>();
            var sender = esClient.CreateSender().Build();
            var typeName = typeof(T).Name;
            var reqName = typeof(TReq).Name;
            router.MapGroup("api")
                .MapPost(typeName + "/{id}/" + reqName, async (string id, TReq req)  =>
                {
                    var response = await sender.SendAndReceiveAsync<T>(new Request(id, req));
                    var statusCode = response.First().StatusCode;
                    return statusCode switch
                    {
                        HttpStatusCode.OK => Results.Ok(response),
                        HttpStatusCode.Created => Results.Created($"{typeName}/{id}", response),
                        _ => Results.StatusCode((int)statusCode)
                    };
                })
                .WithTags(typeName);
        }

        private static void MapCommand<TCmd, T>(this IEndpointRouteBuilder router)
            where T : class, IState, new()
            where TCmd : ICommand<T>
        {
            var sender = router.ServiceProvider.GetRequiredService<EventSourcingClient<T>>().CreateSender().Build();

            var typeName = typeof(T).Name;
            var reqName = typeof(TCmd).Name;
            router.MapGroup("api")
                .MapPost(typeName + "/{id}/" + reqName, async (string id, TCmd cmd)  =>
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
