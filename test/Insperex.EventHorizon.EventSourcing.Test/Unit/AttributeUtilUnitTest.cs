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
        Assert.Equal("test_snapshot_bank_account", attribute.BucketId);
        Assert.Equal("Account", attribute.Type);
    }
    
    [Fact]
    public void TestSetBucketFromState()
    {
        var origAttr = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        _attributeUtil.Set(_type, new SnapshotStoreAttribute("temp", _type.Name));
        var newAttr = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        
        // Assert
        Assert.Equal("temp", newAttr.BucketId);
        Assert.Equal("Account", newAttr.Type);
        
        // Restore
        _attributeUtil.Set(_type, new SnapshotStoreAttribute(origAttr.BucketId, _type.Name));
        var newAttr2 = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        Assert.Equal(origAttr.BucketId, newAttr2.BucketId);
        Assert.Equal(origAttr.Type, newAttr2.Type);
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
        _attributeUtil.Set(_type, new SnapshotStoreAttribute(origBucketId, _type.Name));
        var newAttr2 = _attributeUtil.GetOne<SnapshotStoreAttribute>(_type);
        Assert.Equal(origBucketId, newAttr2.BucketId);
    }
}