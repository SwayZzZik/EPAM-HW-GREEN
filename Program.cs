using GreenAndTasty.Backend.Endpoints;
using GreenAndTasty.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DemoRepository>();
builder.Services.AddSingleton<DemoAuthentication>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("HealthCheck");

app.MapGet("/", () => Results.Redirect("/swagger/index.html"))
   .ExcludeFromDescription();

app.MapGet("/guest/groups/{groupReservationId}/sessions", GroupEndpoints.GetSessions)
   .WithName("GetGroupSessions");

app.MapGet("/reservations/{reservationId}/guest-session-id", GroupEndpoints.GetOwnerSessionId)
   .WithName("GetOwnerSessionId");

app.MapPost("/group-reservations/{groupReservationId}/submit", GroupEndpoints.SubmitGroupOrder)
   .WithName("SubmitGroupOrder");

app.MapDelete("/guest/groups/{groupReservationId}/sessions/{guestSessionId}", GroupEndpoints.DeleteGuestSession)
   .WithName("DeleteGuestSession");

app.Run();
