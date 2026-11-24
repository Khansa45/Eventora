namespace Eventora_Project.Repositories.Results
{
    public enum EventEditOutcome
    {
        Updated,
        NotFoundOrNotOwned,
        CapacityBelowRegistrations
    }

    public sealed record EventEditResult(EventEditOutcome Outcome, int ConfirmedRegistrations);
}
