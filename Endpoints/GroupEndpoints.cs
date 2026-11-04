using GreenAndTasty.Backend.Models;
using GreenAndTasty.Backend.Services;

namespace GreenAndTasty.Backend.Endpoints;

public static class GroupEndpoints
{
    // LEVEL 1: a complete snapshot of every session, including per-guest and group totals.
    public static Task<IResult> GetSessions(
        string groupReservationId, HttpContext context,
        DemoRepository repository, DemoAuthentication authentication)
    {
        var userId = authentication.GetUserId(context);
        if (userId is null) return Task.FromResult<IResult>(Results.Unauthorized());

        return repository.ReadAsync(state =>
        {
            var group = state.GroupReservations.FirstOrDefault(g => g.Id == groupReservationId);
            if (group is null) return NotFound("Group reservation not found.");
            var reservation = state.Reservations.FirstOrDefault(r => r.Id == group.ReservationId);
            if (reservation is null) return NotFound("Reservation not found.");
            if (reservation.OwnerUserId != userId) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(BuildSnapshot(state, group));
        });
    }

    // LEVEL 2a: find the Owner's own guest session for a specific reservation.
    public static Task<IResult> GetOwnerSessionId(
        string reservationId, HttpContext context,
        DemoRepository repository, DemoAuthentication authentication)
    {
        var userId = authentication.GetUserId(context);
        if (userId is null) return Task.FromResult<IResult>(Results.Unauthorized());

        return repository.ReadAsync(state =>
        {
            var reservation = state.Reservations.FirstOrDefault(r => r.Id == reservationId);
            if (reservation is null) return NotFound("Reservation not found.");
            if (reservation.OwnerUserId != userId) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var group = state.GroupReservations.FirstOrDefault(g =>
                g.Id == reservation.GroupReservationId && g.ReservationId == reservation.Id);
            if (group is null) return NotFound("Group reservation not found.");

            var ownerSession = group.Sessions.FirstOrDefault(s =>
                s.Type.Equals("Owner", StringComparison.OrdinalIgnoreCase));
            return ownerSession is null
                ? NotFound("Owner session not found.")
                : Results.Ok(new OwnerSessionResponse(ownerSession.Id));
        });
    }

    // LEVEL 2b: consolidate only submitted sessions, then update Order and Group statuses.
    public static Task<IResult> SubmitGroupOrder(
        string groupReservationId, SubmitGroupRequest request, HttpContext context,
        DemoRepository repository, DemoAuthentication authentication)
    {
        var userId = authentication.GetUserId(context);
        if (userId is null) return Task.FromResult<IResult>(Results.Unauthorized());

        return repository.UpdateAsync(state =>
        {
            var group = state.GroupReservations.FirstOrDefault(g => g.Id == groupReservationId);
            if (group is null) return Unchanged(NotFound("Group reservation not found."));

            var reservation = state.Reservations.FirstOrDefault(r => r.Id == group.ReservationId);
            if (reservation is null) return Unchanged(NotFound("Reservation not found."));
            if (reservation.OwnerUserId != userId) return Unchanged(Results.StatusCode(StatusCodes.Status403Forbidden));

            if (string.IsNullOrWhiteSpace(request.ReservationId) ||
                request.ReservationId != reservation.Id ||
                reservation.GroupReservationId != group.Id)
                return Unchanged(BadRequest("Reservation does not match the group reservation."));

            var submittedSessions = group.Sessions
                .Where(s => s.Type.Equals("Guest", StringComparison.OrdinalIgnoreCase) &&
                            s.Status.Equals("Submitted", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (submittedSessions.Count == 0)
                return Unchanged(BadRequest("There are no submitted sessions."));

            var lines = submittedSessions.SelectMany(s => s.Items).ToList();
            if (lines.Count == 0)
                return Unchanged(BadRequest("Submitted sessions contain no dishes."));

            if (lines.Any(i => i.Quantity <= 0 || !state.Dishes.Any(d => d.Id == i.DishId)))
                return Unchanged(BadRequest("Submitted sessions contain an invalid dish or quantity."));

            var order = state.Orders.FirstOrDefault(o =>
                o.Id == group.OrderId && o.ReservationId == reservation.Id);
            if (order is null) return Unchanged(BadRequest("Order not found."));

            var consolidated = lines
                .GroupBy(i => i.DishId, StringComparer.Ordinal)
                .Select(g => new OrderLine { DishId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToList();

            // Replace, rather than append: repeating POST does not duplicate dishes.
            order.Items = consolidated;
            order.Status = "SubmittedByCustomer";
            group.Status = "Submitted";

            var response = consolidated
                .Select(i => new SubmittedDishResponse(i.DishId, i.Quantity))
                .ToArray();
            return (Results.Ok(response), true);
        });
    }

    // LEVEL 3: remove a Guest (never the Owner) and return an up-to-date snapshot.
    public static Task<IResult> DeleteGuestSession(
        string groupReservationId, string guestSessionId, HttpContext context,
        DemoRepository repository, DemoAuthentication authentication)
    {
        var userId = authentication.GetUserId(context);
        if (userId is null) return Task.FromResult<IResult>(Results.Unauthorized());

        return repository.UpdateAsync(state =>
        {
            var group = state.GroupReservations.FirstOrDefault(g => g.Id == groupReservationId);
            if (group is null) return Unchanged(NotFound("Group reservation not found."));
            var reservation = state.Reservations.FirstOrDefault(r => r.Id == group.ReservationId);
            if (reservation is null) return Unchanged(NotFound("Reservation not found."));
            if (reservation.OwnerUserId != userId) return Unchanged(Results.StatusCode(StatusCodes.Status403Forbidden));

            var session = group.Sessions.FirstOrDefault(s => s.Id == guestSessionId);
            if (session is null)
            {
                bool existsInAnotherGroup = state.GroupReservations.Any(g =>
                    g.Id != group.Id && g.Sessions.Any(s => s.Id == guestSessionId));
                return Unchanged(existsInAnotherGroup
                    ? BadRequest("Guest session does not belong to this group reservation.")
                    : NotFound("Guest session not found."));
            }
            if (!session.Type.Equals("Guest", StringComparison.OrdinalIgnoreCase))
                return Unchanged(BadRequest("Only Guest sessions can be removed."));

            group.Sessions.Remove(session);
            return (Results.Ok(BuildSnapshot(state, group)), true);
        });
    }

    private static GroupSnapshotResponse BuildSnapshot(DemoState state, GroupReservation group)
    {
        var dishes = state.Dishes.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var participants = group.Sessions.Select(session =>
        {
            var items = session.Items.Select(item =>
            {
                var dish = dishes[item.DishId];
                return new ParticipantItemResponse(
                    new DishResponse(dish.Id, dish.Name, dish.Price), item.Quantity);
            }).ToArray();

            var total = session.Items.Sum(item => dishes[item.DishId].Price * item.Quantity);
            return new ParticipantResponse(
                session.Id, session.Type, session.Name, items, total,
                session.Status.Equals("Submitted", StringComparison.OrdinalIgnoreCase));
        }).ToArray();

        return new GroupSnapshotResponse(participants, participants.Sum(p => p.Total));
    }

    private static IResult NotFound(string message) => Results.NotFound(new ErrorResponse(message));
    private static IResult BadRequest(string message) => Results.BadRequest(new ErrorResponse(message));
    private static (IResult Response, bool Changed) Unchanged(IResult response) => (response, false);
}
