using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.Abstractions.Testing;

public static class TestUtil
{
    public static void SetTestBucketIds(AttributeUtil attributeUtil, params Type[] types)
    {
        var random = new Random((int)DateTime.UtcNow.Ticks);
        var iteration = $"_{random.Next()}";
        foreach (var type in types)
        {
            var snapAttr = attributeUtil.GetOne<SnapshotStoreAttribute>(type);
            var viewAttr = attributeUtil.GetOne<ViewStoreAttribute>(type);
            var eventAttrs = attributeUtil.GetAll<StreamAttribute<Event>>(type) as BaseStreamAttribute[];
            var commandAttrs = attributeUtil.GetAll<StreamAttribute<Command>>(type) as BaseStreamAttribute[];
            var requestAttrs = attributeUtil.GetAll<StreamAttribute<Request>>(type) as BaseStreamAttribute[];
            var responseAttrs = attributeUtil.GetAll<StreamAttribute<Response>>(type) as BaseStreamAttribute[];
            var streamAttrs = eventAttrs.Concat(commandAttrs).Concat(requestAttrs).Concat(responseAttrs).ToArray();

            if (snapAttr != null) snapAttr.BucketId += iteration;
            if (viewAttr != null) viewAttr.Database += iteration;
            foreach (var streamAttr in streamAttrs)
                streamAttr.Topic += iteration;

            // Update All
            if (snapAttr != null) attributeUtil.Set(type, snapAttr);
            if (viewAttr != null) attributeUtil.Set(type, viewAttr);
            foreach (var streamAttr in streamAttrs)
                attributeUtil.Set(type, streamAttr);
        }
    }
}
