using System.Net;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;

namespace EventHorizon.EventSourcing.Samples.Models.Actions;

// Request
public record OpenAccount(int Amount) : IRequest<Account, AccountResponse>;
public record Withdrawal(int Amount) : IRequest<Account, AccountResponse>;
public record Deposit(int Amount) : IRequest<Account, AccountResponse>;

// Events
public record AccountOpened(int Amount) : IEvent<Account>;
public record AccountDebited(int Amount) : IEvent<Account>;
public record AccountCredited(int Amount) : IEvent<Account>;

// Response
public record AccountResponse(HttpStatusCode StatusCode = HttpStatusCode.OK, string Error = null) : IResponse<Account>;

public static class AccountConstants
{
    public const string WithdrawalDenied = "Insufficient Funds";
}
