using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;

// Request
public record OpenAccount(int Amount) : IRequest<Account, AccountResponse>;
public record Withdrawal(int Amount) : IRequest<Account, AccountResponse>;
public record Deposit(int Amount) : IRequest<Account, AccountResponse>;

// Events
public record AccountOpened(int Amount) : IEvent<Account>;
public record AccountDebited(int Amount) : IEvent<Account>;
public record AccountCredited(int Amount) : IEvent<Account>;

// Response
public record AccountResponse(AccountResponseStatus Status = AccountResponseStatus.Success, string Error = null) : IResponse<Account>;

public enum AccountResponseStatus
{
    Success,
    WithdrawalDenied,
    // ----- Internal Errors Below ----
    CommandTimedOut,
    LoadSnapshotFailed,
    HandlerFailed,
    BeforeSaveFailed,
    AfterSaveFailed,
    SaveSnapshotFailed,
    SaveEventsFailed,
}
