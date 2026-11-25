namespace IdentityTenant.Application.Plans;





public interface IAccessRequestNotifier
{
    Task NotifyAsync(long tenantId, string companyName, AccessRequest request, CancellationToken ct = default);
}
