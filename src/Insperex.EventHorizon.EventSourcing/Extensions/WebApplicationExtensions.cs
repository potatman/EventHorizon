using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing.Extensions
{
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Api Routing for Minimal WebApis
        /// </summary>
        public static void MapEventSourcingEndpoints<T>(this WebApplication app) where T : class, IState, new()
        {
            MapEventSourcingEndpoints<T>(app as IEndpointRouteBuilder);
        }

        /// <summary>
        /// Api Routing for Generic WebApis
        /// </summary>
        public static void MapEventSourcingEndpoints<T>(this IEndpointRouteBuilder endpointRouteBuilder) where T : class, IState, new()
        {
            var type = typeof(T);

            // Map Get
            var esClient = endpointRouteBuilder.ServiceProvider.GetRequiredService<EventSourcingClient<T>>();
            var aggregator = esClient.Aggregator().Build();
            var group = endpointRouteBuilder.MapGroup("api");

            group.MapGet(type.Name + "/{id}", async (string id) =>
                {
                    try
                    {
                        var response = await aggregator.GetAggregateFromStateAsync(id, CancellationToken.None);
                        return response.Exists() ? Results.Ok(response.Payload) : Results.NotFound();
                    }
                    catch (Exception e)
                    {
                        return Results.Problem(e.StackTrace, title: e.Message);
                    }
                })
                .WithTags(type.Name)
                .Produces<T>()
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status500InternalServerError);
            group.MapGet(type.Name + "/{id}/state-in-time", async (string id, [FromQuery] DateTime dateTime) =>
                {
                    try
                    {
                        var response = await aggregator.GetAggregateFromStateAsync(id, CancellationToken.None);
                        return response.Exists() ? Results.Ok(response.Payload) : Results.NotFound();
                    }
                    catch (Exception e)
                    {
                        return Results.Problem(e.StackTrace, title: e.Message);
                    }
                })
                .WithTags(type.Name)
                .Produces<T>()
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status500InternalServerError);

            var stateDetail = ReflectionFactory.GetStateDetail(type);

            // Map Requests
            var requests = stateDetail.MessageTypeDict[typeof(Request)].Values
                .Where(x => x.GetInterfaces().Any(i => i.Name == typeof(IRequest<,>).Name &&  i.GetGenericArguments()[0] == type))
                .ToDictionary(x => x, x => x.GetInterfaces().FirstOrDefault(i => i.Name == typeof(IRequest<,>).Name));
            var methodReq = typeof(WebApplicationExtensions).GetMethod("MapRequest", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var request in requests)
            {
                var requestArgs = request.Value.GetGenericArguments();
                var generic = methodReq?.MakeGenericMethod(request.Key, requestArgs[1], requestArgs[0]);
                generic?.Invoke(obj: null, parameters: new object[] { endpointRouteBuilder });
            }

            // Map Commands
            var commands = stateDetail.MessageTypeDict[typeof(Command)].Values
                .Where(x => x.GetInterfaces().Any(i => i.Name == typeof(ICommand<>).Name &&  i.GetGenericArguments()[0] == type))
                .ToDictionary(x => x, x => x.GetInterfaces().FirstOrDefault(i => i.Name == typeof(ICommand<>).Name));
            var methodCmd = typeof(WebApplicationExtensions).GetMethod("MapCommand", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var command in commands)
            {
                var requestArgs = command.Value.GetGenericArguments();
                var generic = methodCmd?.MakeGenericMethod(command.Key, requestArgs[0]);
                generic?.Invoke(obj: null, parameters: new object[] { endpointRouteBuilder });
            }
        }

        private static void MapRequest<TReq, TRes, TState>(IEndpointRouteBuilder endpointRouteBuilder)
            where TState : class, IState, new()
            where TReq : IRequest<TState, TRes>
            where TRes : class, IResponse<TState>
        {
            var esClient = endpointRouteBuilder.ServiceProvider.GetRequiredService<EventSourcingClient<TState>>();
            var sender = esClient.CreateSender().Build();
            var typeName = typeof(TState).Name;
            var reqName = typeof(TReq).Name;
            endpointRouteBuilder.MapGroup("api")
                .MapPost(typeName + "/{id}/" + reqName, async (string id, TReq req)  =>
                {
                    var response = await sender.SendAndReceiveAsync(new Request(id, req));
                    var first = response.First();
                    var statusCode = (HttpStatusCode)first.StatusCode;
                    if ((int)statusCode > 300)
                        return Results.Problem(first.Error, title: first.Error);

                    return statusCode switch
                    {
                        HttpStatusCode.OK => Results.Ok(response),
                        HttpStatusCode.Created => Results.Created($"{typeName}/{id}", response),
                        _ => Results.StatusCode((int)statusCode)
                    };
                })
                .WithTags(typeName)
                .Produces<TRes>()
                .Produces<TRes>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status500InternalServerError);
        }

        private static void MapCommand<TCmd, TState>(IEndpointRouteBuilder endpointRouteBuilder)
            where TState : class, IState, new()
            where TCmd : ICommand<TState>
        {
            var sender = endpointRouteBuilder.ServiceProvider.GetRequiredService<EventSourcingClient<TState>>().CreateSender().Build();

            var typeName = typeof(TState).Name;
            var reqName = typeof(TCmd).Name;
            endpointRouteBuilder.MapGroup("api")
                .MapPost(typeName + "/{id}/" + reqName, async (string id, TCmd cmd)  =>
                {
                    try
                    {
                        await sender.SendAsync(id, cmd);
                        return Results.Ok();
                    }
                    catch (Exception e)
                    {
                        return Results.Problem(e.StackTrace, title: e.Message);
                    }
                })
                .WithTags(typeName)
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status500InternalServerError);
        }
    }
}
