using System;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventStore.Test.Models;
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
        _type = typeof(FormatterTest.AttributeFormatter);
    }

    [Fact]
    public void TestSetBucketFromState()
    {
        var origAttr = _attributeUtil.GetOne<StoreAttribute>(_type);
        _attributeUtil.Set(_type, new StoreAttribute("temp"));
        var newAttr = _attributeUtil.GetOne<StoreAttribute>(_type);

        // Assert
        Assert.Equal("temp", newAttr.Database);

        // Restore
        _attributeUtil.Set(_type, new StoreAttribute(origAttr.Database));
        var newAttr2 = _attributeUtil.GetOne<StoreAttribute>(_type);
        Assert.Equal(origAttr.Database, newAttr2.Database);
    }

    [Fact(Skip = "Skip For Now")]
    public void TestGetPropertyStateAttribute()
    {
        var propertyInfo = _attributeUtil.GetOnePropertyInfo<StreamPartitionKeyAttribute>(typeof(OpenAccount));
        Assert.Equal("BankAccount", propertyInfo.Name);
    }

    [Fact]
    public void TestGetPropertyActionAttribute()
    {
        var propertyInfo = _attributeUtil.GetOnePropertyInfo<StreamPartitionKeyAttribute>(typeof(ChangeUserName));
        Assert.Equal("Name", propertyInfo.Name);
    }
}
