using System;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Xunit;

namespace Insperex.EventHorizon.EventSourcing.Test.Unit;

[Trait("Category", "Unit")]
public class AttributeUtilUnitTest
{
    private readonly AttributeUtil _attributeUtil;
    private readonly Type _type;

    public AttributeUtilUnitTest()
    {
        _attributeUtil = new AttributeUtil();
        _type = typeof(Account);
    }

    [Fact]
    public void TestGetBucketFromState()
    {
        var attribute = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        Assert.Equal("test_bank_snapshot_account", attribute.BucketId);
    }

    [Fact]
    public void TestSetBucketFromState()
    {
        var origAttr = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        _attributeUtil.Set(_type, new SnapshotStoreAttribute("temp"));
        var newAttr = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);

        // Assert
        Assert.Equal("temp", newAttr.BucketId);

        // Restore
        _attributeUtil.Set(_type, new SnapshotStoreAttribute(origAttr.BucketId));
        var newAttr2 = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        Assert.Equal(origAttr.BucketId, newAttr2.BucketId);
    }

    [Fact]
    public void TestSetTestValues()
    {
        var origBucketId = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId;
        TestUtil.SetTestBucketIds(_attributeUtil, _type);
        var newAttr = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);

        // Assert
        Assert.NotEqual(origBucketId, newAttr.BucketId);

        // Restore
        _attributeUtil.Set(_type, new SnapshotStoreAttribute(origBucketId));
        var newAttr2 = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        Assert.Equal(origBucketId, newAttr2.BucketId);
    }
}
