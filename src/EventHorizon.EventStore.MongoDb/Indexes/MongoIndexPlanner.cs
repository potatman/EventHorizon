using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.EventStore.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace EventHorizon.EventStore.MongoDb.Indexes;

internal enum MongoIndexAction
{
    Create,
    Exists,
    Conflict
}

internal readonly record struct MongoIndexPlan(MongoIndexAction Action, string Reason = null);

/// <summary>
/// Translates StoreField intents into index specs and compares them with a collection's existing
/// indexes by key specification (not by name), so equivalent indexes created elsewhere are reused
/// and conflicting ones are reported instead of failing createIndexes.
/// </summary>
internal static class MongoIndexPlanner
{
    /// <summary>MongoDB allows one text index per collection; all FullText fields share it.</summary>
    public const string TextIndexName = "StoreField_text";

    private const string KeyField = "key";
    private const string NameField = "name";
    private const string WeightsField = "weights";
    private const string ExpireAfterSecondsField = "expireAfterSeconds";
    private const string TextKeyField = "_fts";
    private const string Text = "text";

    /// <summary>
    /// ExactMatch/Sortable fields each get an ascending index; FullText fields are combined into
    /// a single text index. NotQueried subtrees are skipped.
    /// </summary>
    public static IReadOnlyList<MongoIndexSpec> BuildIntentIndexes(StoreFieldSchema root, Type documentType)
    {
        var valueFields = new List<string>();
        var textFields = new List<string>();
        CollectIntentPaths(root, documentType, string.Empty, valueFields, textFields);

        var specs = valueFields
            .Distinct(StringComparer.Ordinal)
            .Select(x => new MongoIndexSpec($"{x}_1", new BsonDocument(x, 1)))
            .ToList();

        if (textFields.Count > 0)
            specs.Add(new MongoIndexSpec(TextIndexName,
                new BsonDocument(textFields.Distinct(StringComparer.Ordinal).Select(x => new BsonElement(x, Text)))));

        return specs;
    }

    private static void CollectIntentPaths(StoreFieldSchema node, Type nodeType, string prefix,
        List<string> valueFields, List<string> textFields)
    {
        foreach (var child in node.Children ?? Array.Empty<StoreFieldSchema>())
        {
            if (child.Intent == FieldIntent.NotQueried || !child.HasExplicitIntents) continue;

            var elementName = GetElementName(nodeType, child.Property);
            if (elementName is null) continue;

            var path = prefix.Length == 0 ? elementName : prefix + "." + elementName;
            if ((child.Intent & (FieldIntent.ExactMatch | FieldIntent.Sortable)) != 0)
                valueFields.Add(path);
            if (child.Intent.HasFlag(FieldIntent.FullText))
                textFields.Add(path);

            if (child.Children is not null)
                CollectIntentPaths(child, child.ClrType, path, valueFields, textFields);
        }
    }

    /// <summary>
    /// BSON element name of a member as the driver serializes it (class maps, conventions,
    /// [BsonElement], [BsonId]); null when the member is not serialized. Looking up a class map
    /// registers it, so custom class maps must be registered before stores are set up.
    /// </summary>
    public static string GetElementName(Type type, PropertyInfo property) =>
        GetElementName(type, property.Name, property.DeclaringType);

    public static string GetElementName(Type type, string memberName, Type declaringType = null)
    {
        var memberMaps = BsonClassMap.LookupClassMap(type).AllMemberMaps
            .Where(x => x.MemberName == memberName)
            .ToList();

        var memberMap = memberMaps.Find(x => x.MemberInfo.DeclaringType == declaringType) ?? memberMaps.FirstOrDefault();
        return memberMap?.ElementName;
    }

    public static MongoIndexPlan Plan(MongoIndexSpec desired, IReadOnlyCollection<BsonDocument> existing) =>
        desired.IsText ? PlanText(desired, existing) : PlanKeyed(desired, existing);

    private static MongoIndexPlan PlanText(MongoIndexSpec desired, IReadOnlyCollection<BsonDocument> existing)
    {
        var textIndex = existing.FirstOrDefault(IsTextIndex);
        if (textIndex is null)
            return PlanByName(desired, existing);

        var existingFields = textIndex.TryGetValue(WeightsField, out var weights) && weights.IsBsonDocument
            ? weights.AsBsonDocument.Names.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        if (existingFields.SetEquals(desired.Keys.Names))
            return new MongoIndexPlan(MongoIndexAction.Exists);

        return new MongoIndexPlan(MongoIndexAction.Conflict,
            $"text index '{GetName(textIndex)}' covers [{string.Join(", ", existingFields.OrderBy(x => x, StringComparer.Ordinal))}] " +
            $"but FullText fields are [{string.Join(", ", desired.Keys.Names)}]; a collection allows one text index, drop it to apply the change");
    }

    private static MongoIndexPlan PlanKeyed(MongoIndexSpec desired, IReadOnlyCollection<BsonDocument> existing)
    {
        var match = existing.FirstOrDefault(x => KeysEqual(GetKeys(x), desired.Keys));
        if (match is null)
            return PlanByName(desired, existing);

        var existingTtl = match.TryGetValue(ExpireAfterSecondsField, out var ttl) && ttl.IsNumeric ? ttl.ToInt64() : (long?)null;
        var desiredTtl = desired.ExpireAfter.HasValue ? (long)desired.ExpireAfter.Value.TotalSeconds : (long?)null;
        if (existingTtl == desiredTtl)
            return new MongoIndexPlan(MongoIndexAction.Exists);

        return new MongoIndexPlan(MongoIndexAction.Conflict,
            $"index '{GetName(match)}' on {desired.Keys} has expireAfterSeconds {existingTtl?.ToString(CultureInfo.InvariantCulture) ?? "unset"}, " +
            $"expected {desiredTtl?.ToString(CultureInfo.InvariantCulture) ?? "unset"}; drop it to apply the change");
    }

    private static MongoIndexPlan PlanByName(MongoIndexSpec desired, IReadOnlyCollection<BsonDocument> existing)
    {
        var sameName = existing.FirstOrDefault(x => GetName(x) == desired.Name);
        if (sameName is null)
            return new MongoIndexPlan(MongoIndexAction.Create);

        return new MongoIndexPlan(MongoIndexAction.Conflict,
            $"index '{desired.Name}' already exists with keys {GetKeys(sameName)}, expected {desired.Keys}");
    }

    private static bool IsTextIndex(BsonDocument index)
    {
        var keys = GetKeys(index);
        return keys is not null && keys.TryGetValue(TextKeyField, out var value) && value.IsString && value.AsString == Text;
    }

    private static BsonDocument GetKeys(BsonDocument index) =>
        index.TryGetValue(KeyField, out var keys) && keys.IsBsonDocument ? keys.AsBsonDocument : null;

    private static string GetName(BsonDocument index) =>
        index.TryGetValue(NameField, out var name) && name.IsString ? name.AsString : null;

    private static bool KeysEqual(BsonDocument left, BsonDocument right)
    {
        if (left is null || right is null || left.ElementCount != right.ElementCount) return false;

        for (var i = 0; i < left.ElementCount; i++)
        {
            var l = left.GetElement(i);
            var r = right.GetElement(i);
            if (l.Name != r.Name) return false;

            // Servers may report numeric key directions as int, long or double
            var equal = l.Value.IsNumeric && r.Value.IsNumeric
                ? l.Value.ToDouble().Equals(r.Value.ToDouble())
                : l.Value.Equals(r.Value);
            if (!equal) return false;
        }

        return true;
    }
}
