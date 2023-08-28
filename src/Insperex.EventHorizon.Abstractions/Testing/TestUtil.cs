using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.Abstractions.Testing;

public static class TestUtil
{
    public static void SetTestBucketIds(AttributeUtil attributeUtil, string postfix, params Type[] types)
    {
        foreach (var type in types)
        {
            var snapAttr = attributeUtil.GetOne<SnapshotStoreAttribute>(type);
            var viewAttr = attributeUtil.GetOne<ViewStoreAttribute>(type);
            var streamAttrs = attributeUtil.GetAll<StreamAttribute>(type);

            if (snapAttr != null) snapAttr.BucketId += postfix;
            if (viewAttr != null) viewAttr.Database += postfix;
            foreach (var streamAttr in streamAttrs)
                streamAttr.Topic += postfix;

            // Update All
            if (snapAttr != null) attributeUtil.Set(type, snapAttr);
            if (viewAttr != null) attributeUtil.Set(type, viewAttr);
            foreach (var streamAttr in streamAttrs)
                attributeUtil.Set(type, streamAttr);
        }
    }
}
