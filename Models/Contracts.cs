namespace GreenAndTasty.Backend.Models;

public sealed record ErrorResponse(string Error);

public sealed record DishResponse(string Id, string Name, decimal Price);

public sealed record ParticipantItemResponse(DishResponse Dish, int Quantity);

public sealed record ParticipantResponse(
    string Id,
    string Type,
    string Name,
    IReadOnlyList<ParticipantItemResponse> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record GroupSnapshotResponse(
    IReadOnlyList<ParticipantResponse> Participants,
    decimal Total);

public sealed record OwnerSessionResponse(string GuestSessionId);

public sealed record SubmitGroupRequest(string? ReservationId);

public sealed record SubmittedDishResponse(string DishId, int Quantity);
