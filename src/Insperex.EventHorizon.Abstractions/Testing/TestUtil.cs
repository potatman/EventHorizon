using System;
using Insperex.EventHorizon.Abstractions.Attributes;
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
            var eventAttrs = attributeUtil.GetAll<EventStreamAttribute>(type);

            if (snapAttr != null) snapAttr.BucketId += iteration;
            if (viewAttr != null) viewAttr.BucketId += iteration;
            foreach (var eventAttr in eventAttrs)
                eventAttr.BucketId += iteration;

            // Update All
            if (snapAttr != null) attributeUtil.Set(type, snapAttr);
            if (viewAttr != null) attributeUtil.Set(type, viewAttr);
            foreach (var eventAttr in eventAttrs)
                attributeUtil.Set(type, eventAttr);
        }
    }
}
