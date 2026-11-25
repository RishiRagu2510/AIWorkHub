using Authorization;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Entities;

namespace IdentityTenant.Application.Plans;


public sealed record CompanyPlan(
    IReadOnlyList<PlanPortal> Portals,
    IReadOnlyList<PlanFeature> Features,
    bool IsAdmin,
    IReadOnlyList<AccessRequest> Requests);

public sealed record PlanPortal(string Id, bool Included);


public sealed record PlanFeature(string Id, bool Included, string? Requires);


public sealed record PlanChangeInput(string? Portal, string? Feature, string? Note, string? Action = null);

public enum RequestStatus
{
    
    Created,
    
    Existing,
    BadRequest,
    Forbidden,
    Conflict,
}


public sealed record RequestResult(RequestStatus Status, AccessRequest? Request = null, string? Error = null, string? Code = null, string? Requires = null);


public sealed record PlanSelection(IReadOnlyList<string>? Portals, IReadOnlyList<string>? Features);

public enum SetPlanStatus { Ok, Invalid, TenantNotFound }

public sealed record SetPlanResult(SetPlanStatus Status, PlanSelection? Plan = null, string? Error = null);

public enum DecisionOutcome
{
    
    Done,
    
    NotFound,
    
    NeedsPortal,
    
    LastPortal,
}

public sealed record DecisionResult(DecisionOutcome Outcome, AccessRequest? Request = null, string? Needs = null);


public sealed record RequestForReview(string Company, AccessRequest Request);






public sealed class TenantPlanService(ITenantPlanStore store, IAccessRequestNotifier notifier)
{
    

    
    public async Task<CompanyPlan> GetPlanAsync(long tenantId, long userId, bool isAdmin, CancellationToken ct)
    {
        var row = await store.FindSettingsReadOnlyAsync(tenantId, ct);
        var portals = TenantPlanSettings.ReadList(row?.Settings, TenantPlanSettings.PortalsKey);
        var features = TenantPlanSettings.ReadList(row?.Settings, TenantPlanSettings.FeaturesKey);
        
        var open = TenantAccessRequests.StillOpen(TenantAccessRequests.Read(row?.Settings), portals, features);
        var visible = isAdmin ? open : open.Where(r => r.UserId == userId).ToList();

        return new CompanyPlan(
            TenantAccessRequests.Portals.Select(p => new PlanPortal(p, PortalLicense.IsLicensed(portals, p))).ToList(),
            
            [new PlanFeature(FeatureLicense.Athenna, FeatureLicense.IsLicensed(features.ToArray(), portals.ToArray(), FeatureLicense.Athenna), FeatureLicense.RequiredPortal(FeatureLicense.Athenna))],
            isAdmin,
            visible.OrderByDescending(r => r.CreatedAt).ToList());
    }

    



    public async Task<AccessRequest?> CancelRequestAsync(long tenantId, long userId, bool isAdmin, string requestId, CancellationToken ct)
    {
        var row = await store.FindSettingsAsync(tenantId, ct);
        var all = TenantAccessRequests.Read(row?.Settings);
        var target = all.FirstOrDefault(r => r.Id == requestId);
        if (row is null || target is null || (target.UserId != userId && !isAdmin)) return null;

        all.Remove(target);
        var settings = new Dictionary<string, object?>(row.Settings);
        TenantAccessRequests.Write(settings, all);
        row.Update(row.Timezone, row.Locale, settings);
        await store.SaveAsync(ct);
        return target;
    }

    
    public async Task<RequestResult> RequestAsync(long tenantId, long userId, bool isAdmin, PlanChangeInput? input, CancellationToken ct)
    {
        if (input is null) return new(RequestStatus.BadRequest, Error: "Say which portal or feature you need.");

        var action = string.IsNullOrWhiteSpace(input.Action) ? TenantAccessRequests.AddAction : input.Action.Trim().ToLowerInvariant();
        if (action is not (TenantAccessRequests.AddAction or TenantAccessRequests.RemoveAction))
            return new(RequestStatus.BadRequest, Error: "Action must be add or remove.");
        var cancelling = action == TenantAccessRequests.RemoveAction;
        
        if (cancelling && !isAdmin)
            return new(RequestStatus.Forbidden, Error: "Only a company admin can ask to cancel part of the plan.", Code: "ADMIN_ONLY");

        var hasPortal = !string.IsNullOrWhiteSpace(input.Portal);
        var hasFeature = !string.IsNullOrWhiteSpace(input.Feature);
        if (hasPortal == hasFeature) return new(RequestStatus.BadRequest, Error: "Ask for one portal or one feature at a time.");

        string kind, name;
        if (hasPortal)
        {
            if (!PortalLicense.IsKnown(input.Portal!)) return new(RequestStatus.BadRequest, Error: $"Unknown portal: {input.Portal!.Trim()}.");
            kind = TenantAccessRequests.PortalKind;
            name = PortalLicense.NamesFor(input.Portal!)[0];
        }
        else
        {
            if (!FeatureLicense.IsKnown(input.Feature!)) return new(RequestStatus.BadRequest, Error: $"Unknown feature: {input.Feature!.Trim()}.");
            kind = TenantAccessRequests.FeatureKind;
            name = input.Feature!.Trim().ToLowerInvariant();
        }

        var note = input.Note?.Trim();
        if (note is { Length: > TenantAccessRequests.MaxNoteLength })
            return new(RequestStatus.BadRequest, Error: $"Keep the note under {TenantAccessRequests.MaxNoteLength} characters.");
        if (string.IsNullOrEmpty(note)) note = null;

        var row = await store.FindSettingsAsync(tenantId, ct);
        var portals = TenantPlanSettings.ReadList(row?.Settings, TenantPlanSettings.PortalsKey);
        var features = TenantPlanSettings.ReadList(row?.Settings, TenantPlanSettings.FeaturesKey);
        if (cancelling)
        {
            if (!TenantAccessRequests.IsIncluded(kind, name, portals, features))
                return new(RequestStatus.Conflict, Error: "Your plan does not include this.", Code: "NOT_INCLUDED");
            if (kind == TenantAccessRequests.PortalKind && TenantAccessRequests.IncludedPortals(portals).Count <= 1)
                return new(RequestStatus.Conflict, Error: "A plan has to include at least one portal. To end the plan completely, contact your AIWorkHub representative.", Code: "LAST_PORTAL");
        }
        else
        {
            
            if (kind == TenantAccessRequests.FeatureKind && FeatureLicense.RequiredPortal(name) is { } needs && !PortalLicense.IsLicensed(portals, needs))
                return new(RequestStatus.BadRequest,
                    Error: $"This comes with {PortalLicense.NamesFor(needs)[0]}. Ask for {PortalLicense.NamesFor(needs)[0]} instead.",
                    Code: "REQUIRES_PORTAL", Requires: needs);
            if (TenantAccessRequests.IsIncluded(kind, name, portals, features))
                return new(RequestStatus.Conflict, Error: "Your plan already includes this.", Code: "ALREADY_INCLUDED");
        }

        var open = TenantAccessRequests.StillOpen(TenantAccessRequests.Read(row?.Settings), portals, features);
        
        if (open.FirstOrDefault(r => r.UserId == userId && r.Kind == kind && r.Name == name && r.IsCancellation == cancelling) is { } existing)
            return new(RequestStatus.Existing, existing);

        var userName = await store.UserDisplayNameAsync(tenantId, userId, ct) ?? $"User {userId}";
        var request = new AccessRequest(Guid.NewGuid().ToString("N"), kind, name, userId, userName, note, DateTimeOffset.UtcNow, action);
        open.Add(request);
        if (open.Count > TenantAccessRequests.MaxOpen) open = open.OrderByDescending(r => r.CreatedAt).Take(TenantAccessRequests.MaxOpen).ToList();

        var settings = row is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(row.Settings);
        TenantAccessRequests.Write(settings, open);
        if (row is null)
        {
            row = new TenantSettings(tenantId);
            row.Update(row.Timezone, row.Locale, settings);
            store.AddSettings(row);
        }
        else row.Update(row.Timezone, row.Locale, settings);
        await store.SaveAsync(ct);

        
        
        var companyName = await store.TenantNameAsync(tenantId, ct) ?? $"Tenant {tenantId}";
        _ = notifier.NotifyAsync(tenantId, companyName, request, CancellationToken.None);
        return new(RequestStatus.Created, request);
    }

    

    
    public async Task<PlanSelection?> DescribePlanAsync(long tenantId, CancellationToken ct)
    {
        if (!await store.TenantExistsAsync(tenantId, ct)) return null;
        var settings = await store.FindSettingsReadOnlyAsync(tenantId, ct);
        return Describe(settings?.Settings);
    }

    
    public async Task<IReadOnlyList<AccessRequest>?> OpenRequestsAsync(long tenantId, CancellationToken ct)
    {
        if (!await store.TenantExistsAsync(tenantId, ct)) return null;
        var settings = await store.FindSettingsReadOnlyAsync(tenantId, ct);
        var open = TenantAccessRequests.StillOpen(TenantAccessRequests.Read(settings?.Settings),
            TenantPlanSettings.ReadList(settings?.Settings, TenantPlanSettings.PortalsKey),
            TenantPlanSettings.ReadList(settings?.Settings, TenantPlanSettings.FeaturesKey));
        return open.OrderByDescending(r => r.CreatedAt).ToList();
    }

    
    public async Task<RequestForReview?> FindRequestForReviewAsync(long tenantId, string requestId, CancellationToken ct)
    {
        var company = await store.TenantNameAsync(tenantId, ct);
        if (company is null) return null;
        var settings = await store.FindSettingsReadOnlyAsync(tenantId, ct);
        var request = TenantAccessRequests.Read(settings?.Settings).FirstOrDefault(r => r.Id == requestId);
        return request is null ? null : new RequestForReview(company, request);
    }

    



    public async Task<SetPlanResult> SetPlanAsync(long tenantId, IReadOnlyList<string>? requestedPortals, IReadOnlyList<string>? requestedFeatures, CancellationToken ct)
    {
        var (portals, features, error) = TenantPlanSettings.Normalize(requestedPortals, requestedFeatures);
        if (error is not null) return new(SetPlanStatus.Invalid, Error: error);
        if (!await store.TenantExistsAsync(tenantId, ct)) return new(SetPlanStatus.TenantNotFound);

        var row = await store.FindSettingsAsync(tenantId, ct);
        var settings = row is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(row.Settings);
        if (portals is null) settings.Remove(TenantPlanSettings.PortalsKey); else settings[TenantPlanSettings.PortalsKey] = portals;
        if (features is null) settings.Remove(TenantPlanSettings.FeaturesKey); else settings[TenantPlanSettings.FeaturesKey] = features;
        
        TenantAccessRequests.Write(settings, TenantAccessRequests.StillOpen(TenantAccessRequests.Read(settings), portals ?? [], features ?? []));

        if (row is null)
        {
            row = new TenantSettings(tenantId);
            row.Update(row.Timezone, row.Locale, settings);
            store.AddSettings(row);
        }
        else row.Update(row.Timezone, row.Locale, settings);
        await store.SaveAsync(ct);
        return new(SetPlanStatus.Ok, new PlanSelection(portals, features));
    }

    



    public async Task<DecisionResult> ApproveAsync(long tenantId, string requestId, CancellationToken ct)
    {
        var row = await store.FindSettingsAsync(tenantId, ct);
        var requests = TenantAccessRequests.Read(row?.Settings);
        var target = requests.FirstOrDefault(r => r.Id == requestId);
        if (row is null || target is null) return new DecisionResult(DecisionOutcome.NotFound);

        var portals = TenantPlanSettings.ReadList(row.Settings, TenantPlanSettings.PortalsKey);
        var features = TenantPlanSettings.ReadList(row.Settings, TenantPlanSettings.FeaturesKey);

        if (target.IsCancellation)
        {
            if (!Remove(target, ref portals, ref features)) return new DecisionResult(DecisionOutcome.LastPortal, target);
        }
        
        else if (target.Kind == TenantAccessRequests.PortalKind)
        {
            if (portals.Count > 0 && !PortalLicense.IsLicensed(portals, target.Name)) portals.Add(target.Name);
        }
        else
        {
            var needs = FeatureLicense.RequiredPortal(target.Name);
            if (needs is not null && !PortalLicense.IsLicensed(portals, needs)) return new DecisionResult(DecisionOutcome.NeedsPortal, target, needs);
            if (features.Count > 0)
            {
                
                features = features.Any(f => string.Equals(f, FeatureLicense.None, StringComparison.OrdinalIgnoreCase)) ? [target.Name] : features;
                if (!features.Contains(target.Name, StringComparer.OrdinalIgnoreCase)) features.Add(target.Name);
            }
        }

        var (canonicalPortals, canonicalFeatures, error) = TenantPlanSettings.Normalize(portals.Count == 0 ? null : portals, features.Count == 0 ? null : features);
        if (error is not null) return new DecisionResult(DecisionOutcome.NotFound);

        var settings = new Dictionary<string, object?>(row.Settings);
        if (canonicalPortals is null) settings.Remove(TenantPlanSettings.PortalsKey); else settings[TenantPlanSettings.PortalsKey] = canonicalPortals;
        if (canonicalFeatures is null) settings.Remove(TenantPlanSettings.FeaturesKey); else settings[TenantPlanSettings.FeaturesKey] = canonicalFeatures;
        
        TenantAccessRequests.Write(settings, TenantAccessRequests.StillOpen(requests, canonicalPortals ?? [], canonicalFeatures ?? []));
        row.Update(row.Timezone, row.Locale, settings);
        await store.SaveAsync(ct);
        return new DecisionResult(DecisionOutcome.Done, target);
    }

    
    public async Task<DecisionResult> DeclineAsync(long tenantId, string requestId, CancellationToken ct)
    {
        var row = await store.FindSettingsAsync(tenantId, ct);
        var requests = TenantAccessRequests.Read(row?.Settings);
        var target = requests.FirstOrDefault(r => r.Id == requestId);
        if (row is null || target is null) return new DecisionResult(DecisionOutcome.NotFound);

        requests.Remove(target);
        var settings = new Dictionary<string, object?>(row.Settings);
        TenantAccessRequests.Write(settings, requests);
        row.Update(row.Timezone, row.Locale, settings);
        await store.SaveAsync(ct);
        return new DecisionResult(DecisionOutcome.Done, target);
    }

    private static PlanSelection Describe(IDictionary<string, object?>? settings)
    {
        var portals = TenantPlanSettings.ReadList(settings, TenantPlanSettings.PortalsKey);
        var features = TenantPlanSettings.ReadList(settings, TenantPlanSettings.FeaturesKey);
        return new PlanSelection(portals.Count == 0 ? null : portals, features.Count == 0 ? null : features);
    }

    





    private static bool Remove(AccessRequest target, ref List<string> portals, ref List<string> features)
    {
        if (target.Kind == TenantAccessRequests.PortalKind)
        {
            var remaining = TenantAccessRequests.IncludedPortals(portals).Where(p => !PortalLicense.IsLicensed([target.Name], p)).ToList();
            if (remaining.Count == 0) return false;
            portals = remaining;
            if (PortalLicense.IsLicensed([target.Name], PortalLicense.Hub) && features.Count > 0 && !features.Contains(FeatureLicense.None, StringComparer.OrdinalIgnoreCase))
            {
                var kept = features.Where(f => FeatureLicense.RequiredPortal(f) != PortalLicense.Hub).ToList();
                features = kept.Count == 0 ? [FeatureLicense.None] : kept;
            }
        }
        else
        {
            var current = features.Count == 0 ? [FeatureLicense.Athenna] : features.Where(f => !string.Equals(f, FeatureLicense.None, StringComparison.OrdinalIgnoreCase)).ToList();
            var kept = current.Where(f => !string.Equals(f, target.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            features = kept.Count == 0 ? [FeatureLicense.None] : kept;
        }
        return true;
    }
}
