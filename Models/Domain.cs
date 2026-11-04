namespace GreenAndTasty.Backend.Models;

public sealed class DemoState
{
    public List<Dish> Dishes { get; set; } = [];
    public List<Location> Locations { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public List<GroupReservation> GroupReservations { get; set; } = [];
    public List<RestaurantOrder> Orders { get; set; } = [];
}

public sealed class Dish
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string LocationId { get; set; } = "";
}

public sealed class Location
{
    public string Id { get; set; } = "";
    public string Address { get; set; } = "";
}

public sealed class Reservation
{
    public string Id { get; set; } = "";
    public string GroupReservationId { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
}

public sealed class GroupReservation
{
    public string Id { get; set; } = "";
    public string ReservationId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public List<GuestSession> Sessions { get; set; } = [];
}

public sealed class GuestSession
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "Guest";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public List<OrderLine> Items { get; set; } = [];
}

public sealed class RestaurantOrder
{
    public string Id { get; set; } = "";
    public string ReservationId { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public List<OrderLine> Items { get; set; } = [];
}

public sealed class OrderLine
{
    public string DishId { get; set; } = "";
    public int Quantity { get; set; }
}
