using System;
using System.Collections.Concurrent;
using Elastic.Clients.Elasticsearch.IndexManagement;
using EventHorizon.Abstractions.Interfaces;

namespace EventHorizon.EventStore.ElasticSearch.Models;

public class ElasticConfig
{
    public string[] Uris { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }

    private readonly ConcurrentDictionary<Type, Action<CreateIndexRequest>> _indexOverrides = new();

    /// <summary>
    /// Escape hatch for index creation of TState's snapshot/view indices. The hook receives the
    /// request already populated with the generated <see cref="CreateIndexRequest.Mappings"/> and
    /// the <see cref="Attributes.ElasticIndexAttribute"/> <see cref="CreateIndexRequest.Settings"/>;
    /// adjust those objects in place (e.g. <c>request.Settings.NumberOfShards = 4</c>,
    /// <c>(request.Mappings.Properties ??= new Properties()).Add(...)</c>). Assigning a new
    /// mapping or settings object discards the generated one. Only applies when the index is
    /// first created.
    /// </summary>
    public ElasticConfig ConfigureIndex<TState>(Action<CreateIndexRequest> configure)
        where TState : class, IState
    {
        _indexOverrides[typeof(TState)] = configure;
        return this;
    }

    internal Action<CreateIndexRequest> GetIndexOverride(Type stateType) =>
        _indexOverrides.TryGetValue(stateType, out var configure) ? configure : null;
}
