using HealthERP.Application.Common.Interfaces;
using HealthERP.Application.StaffMembers.Models;
using HealthERP.Domain.StaffMembers;
using MediatR;

namespace HealthERP.Application.StaffMembers.Commands.ManageStaffAccount;

public record LinkStaffAccountCommand(Guid StaffId, Guid UserId, StaffKind StaffKind) : IRequest<StaffAccountLinkDto>;
public record UnlinkStaffAccountCommand(Guid StaffId) : IRequest;
public sealed class LinkStaffAccountCommandHandler(IStaffAccountService accounts) : IRequestHandler<LinkStaffAccountCommand, StaffAccountLinkDto>
{
    public Task<StaffAccountLinkDto> Handle(LinkStaffAccountCommand request, CancellationToken ct)
        => accounts.LinkAsync(request.StaffId, request.UserId, request.StaffKind, ct);
}
public sealed class UnlinkStaffAccountCommandHandler(IStaffAccountService accounts) : IRequestHandler<UnlinkStaffAccountCommand>
{
    public Task Handle(UnlinkStaffAccountCommand request, CancellationToken ct) => accounts.UnlinkAsync(request.StaffId, ct);
}
