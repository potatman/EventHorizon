using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventSourcing.Util;
using Xunit;

namespace Insperex.EventHorizon.EventSourcing.Test.Unit;

[Trait("Category", "Unit")]
public class ValidationUtilUnitTest
{
    private readonly ValidationUtil _validationUtil;

    public ValidationUtilUnitTest()
    {
        _validationUtil = new ValidationUtil(new AttributeUtil());
    }

    [Fact]
    public void TestSnapshotPassed()
    {
        _validationUtil.ValidateSnapshot<Account>();
        _validationUtil.ValidateSnapshot<User>();
    }

    [Fact]
    public void TestViewPassed()
    {
        // _validationUtil.ValidateView<AccountView>();
        _validationUtil.ValidateView<SearchAccountView>();
    }

    [Fact]
    public void TestMissingCommandHandlerFails()
    {
        Assert.Throws<MissingHandlersException>(() =>
        {
            _validationUtil.ValidateSnapshot<MissingCommandHandler>();
        });
    }

    [Fact]
    public void TestMissingRequestHandlerFails()
    {
        Assert.Throws<MissingHandlersException>(() =>
        {
            _validationUtil.ValidateSnapshot<MissingRequestHandler>();
        });
    }

    [Fact]
    public void TestMissingEventHandlerFails()
    {
        Assert.Throws<MissingHandlersException>(() =>
        {
            _validationUtil.ValidateSnapshot<MissingEventHandler>();
        });
    }

}

public class MissingCommandHandler : IState
{
    public string Id { get; set; }

    public record MissingCommand(string Id) : ICommand<MissingCommandHandler>;
}

public class MissingRequestHandler : IState
{
    public string Id { get; set; }

    public record MissingRequest(string Id) : IRequest<MissingRequestHandler, MissingResponse>;
    public record MissingResponse(string Id) : IResponse<MissingRequestHandler>;
}

public class MissingEventHandler : IState
{
    public string Id { get; set; }

    public record MissingEvent(string Id) : IEvent<MissingEventHandler>;
}
