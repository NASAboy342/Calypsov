namespace Calypsov.Models;

public sealed record ProfilesResponse(IReadOnlyList<ProtectionProfile> Profiles, Guid CurrentProfileId);
