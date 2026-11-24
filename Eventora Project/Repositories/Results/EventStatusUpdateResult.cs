namespace Eventora_Project.Repositories.Results
{
    public enum EventStatusUpdateResultKind
    {
        Updated,
        NotFound,
        Conflict
    }

    public sealed record EventStatusUpdateResult(EventStatusUpdateResultKind Kind, string? Title);
}
