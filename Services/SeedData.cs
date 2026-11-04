using System.Globalization;
using System.Text.Json;
using GreenAndTasty.Backend.Models;

namespace GreenAndTasty.Backend.Services;

public static class SeedData
{
    public static DemoState Create(string contentRoot)
    {
        var locations = ReadDynamoItems(Path.Combine(contentRoot, "Data", "locations.json"), "Locations")
            .Select(item => new Location
            {
                Id = GetString(item, "id"),
                Address = GetString(item, "address")
            }).ToList();

        var dishes = ReadDynamoItems(Path.Combine(contentRoot, "Data", "dishes.json"), "Dishes")
            .Select(item => new Dish
            {
                Id = GetString(item, "id"),
                Name = GetString(item, "name"),
                Price = decimal.Parse(GetNumber(item, "price"), CultureInfo.InvariantCulture),
                LocationId = GetString(item, "locationId")
            }).ToList();

        // These sessions and reservations are demo fixtures: the provided JSON files
        // include only the catalog of dishes and restaurant locations.
        const string salmon = "15673599-3a6f-44fc-8b76-e64f2f4f2ce3";
        const string risotto = "95f0b087-4128-428f-bd3c-cc3353471e75";
        const string salad = "3a6058ab-493f-4dc7-b2df-3a492be1268d";

        var firstLocation = locations[0].Id;
        foreach (var dishId in new[] { salmon, risotto, salad })
            if (!dishes.Any(d => d.Id == dishId && d.LocationId == firstLocation))
                throw new InvalidDataException($"Missing demo dish: {dishId}");

        return new DemoState
        {
            Locations = locations,
            Dishes = dishes,
            Reservations =
            [
                new Reservation
                {
                    Id = "reservation-123",
                    GroupReservationId = "group-123",
                    OwnerUserId = "owner-user-123"
                },
                new Reservation
                {
                    Id = "reservation-456",
                    GroupReservationId = "group-456",
                    OwnerUserId = "owner-user-123"
                },
                new Reservation
                {
                    Id = "reservation-789",
                    GroupReservationId = "group-789",
                    OwnerUserId = "owner-user-123"
                },
                new Reservation
                {
                    Id = "reservation-999",
                    GroupReservationId = "group-999",
                    OwnerUserId = "owner-user-123"
                },
                new Reservation
                {
                    Id = "reservation-321",
                    GroupReservationId = "group-321",
                    OwnerUserId = "owner-user-123"
                }
            ],
            GroupReservations =
            [
                new GroupReservation
                {
                    Id = "group-123",
                    ReservationId = "reservation-123",
                    OrderId = "order-123",
                    Sessions =
                    [
                        new GuestSession
                        {
                            Id = "owner-session-123", Type = "Owner", Name = "Owner", Status = "Draft"
                        },
                        new GuestSession
                        {
                            Id = "session1", Type = "Guest", Name = "Andrii", Status = "Submitted",
                            Items =
                            [
                                new OrderLine { DishId = salmon, Quantity = 2 },
                                new OrderLine { DishId = salad, Quantity = 1 }
                            ]
                        },
                        new GuestSession
                        {
                            Id = "session2", Type = "Guest", Name = "Olena", Status = "Submitted",
                            Items =
                            [
                                new OrderLine { DishId = salmon, Quantity = 1 },
                                new OrderLine { DishId = risotto, Quantity = 1 }
                            ]
                        },
                        new GuestSession
                        {
                            Id = "session3", Type = "Guest", Name = "Marta", Status = "Draft",
                            Items = [new OrderLine { DishId = salad, Quantity = 2 }]
                        }
                    ]
                },
                new GroupReservation
                {
                    Id = "group-456", ReservationId = "reservation-456", OrderId = "order-456",
                    Sessions =
                    [
                        new GuestSession { Id = "owner-session-456", Type = "Owner", Status = "Draft" },
                        new GuestSession { Id = "other-session-456", Name = "Petro", Status = "Draft" }
                    ]
                },
                new GroupReservation
                {
                    Id = "group-789", ReservationId = "reservation-789", OrderId = "order-789",
                    Sessions = [new GuestSession { Id = "empty-session-789", Name = "Iryna", Status = "Submitted" }]
                },
                new GroupReservation
                {
                    Id = "group-999", ReservationId = "reservation-999", OrderId = "missing-order",
                    Sessions = [new GuestSession
                    {
                        Id = "session-999", Name = "Danylo", Status = "Submitted",
                        Items = [new OrderLine { DishId = salmon, Quantity = 1 }]
                    }]
                },
                // An Owner session must never be counted as a submitted Guest session.
                new GroupReservation
                {
                    Id = "group-321", ReservationId = "reservation-321", OrderId = "order-321",
                    Sessions =
                    [
                        new GuestSession
                        {
                            Id = "owner-session-321", Type = "Owner", Status = "Submitted",
                            Items = [new OrderLine { DishId = salmon, Quantity = 1 }]
                        },
                        new GuestSession { Id = "guest-session-321", Type = "Guest", Status = "Draft" }
                    ]
                }
            ],
            Orders =
            [
                new RestaurantOrder { Id = "order-123", ReservationId = "reservation-123" },
                new RestaurantOrder { Id = "order-456", ReservationId = "reservation-456" },
                new RestaurantOrder { Id = "order-789", ReservationId = "reservation-789" },
                new RestaurantOrder { Id = "order-321", ReservationId = "reservation-321" }
            ]
        };
    }

    private static List<JsonElement> ReadDynamoItems(string path, string rootProperty)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty(rootProperty)
            .EnumerateArray()
            .Select(element => element.GetProperty("PutRequest").GetProperty("Item").Clone())
            .ToList();
    }

    private static string GetString(JsonElement item, string key) =>
        item.GetProperty(key).GetProperty("S").GetString()
        ?? throw new InvalidDataException($"Empty field: {key}");

    private static string GetNumber(JsonElement item, string key) =>
        item.GetProperty(key).GetProperty("N").GetString()
        ?? throw new InvalidDataException($"Empty numeric field: {key}");
}
