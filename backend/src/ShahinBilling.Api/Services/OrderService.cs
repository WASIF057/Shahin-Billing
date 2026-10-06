using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class OrderService(MongoContext db, InvoiceNumberService numbers)
{
    public const int MaxLines = 50;
    public const decimal MaxQuantity = 10000;

    public static string OrderNumber(int seq) => "ORD-" + seq.ToString("D4");

    public static string Label(Item i) => string.IsNullOrWhiteSpace(i.SizeOrVariant) ? i.Name : $"{i.Name} \u2014 {i.SizeOrVariant}";

    public static PortalOrderDto ToPortal(Order o) => new(o.Id, o.OrderNumber, o.CreatedAt, o.Status, o.City, o.Note, o.CancelReason,
        o.Lines.Select(l => new PortalOrderLineDto(l.Label, l.Quantity, l.Unit)).ToList(), o.Priority);

    // ---------------------------------------------------------------- client side
    /// <summary>Places an order for the logged-in client. Products must exist and be active in this business; quantities are checked.</summary>
    public async Task<Order> PlaceAsync(string businessId, string clientId, string userId, string userName, string userEmail, PlaceOrderRequest req)
    {
        var client = await db.Clients.Find(c => c.BusinessId == businessId && c.Id == clientId).FirstOrDefaultAsync()
                     ?? throw new NotFoundException("Client");
        if (!client.IsActive) throw new AppException("Ordering is switched off for your account. Please contact us.", 403);
        return await CreateAsync(businessId, client, req.Lines, req.City, req.Note, req.Priority, OrderSource.Website, userId, userName, userEmail, "");
    }

    /// <summary>An order owner or staff take when the client phones in. It starts Accepted (they took it themselves) and never rings the alert.</summary>
    public async Task<Order> TakeAsync(string businessId, string userId, string userName, TakeOrderRequest req)
    {
        var client = await db.Clients.Find(c => c.BusinessId == businessId && c.Id == req.ClientId).FirstOrDefaultAsync()
                     ?? throw new AppException("Choose the client who is ordering.");
        if (!client.IsActive) throw new AppException("That client is switched off. Switch them on in Clients first.");
        return await CreateAsync(businessId, client, req.Lines, req.City, req.Note, req.Priority, OrderSource.Phone, userId, userName, client.Email ?? "", userName);
    }

    private async Task<Order> CreateAsync(string businessId, Client client, List<OrderLineRequest>? requested, string? cityIn, string? noteIn,
        OrderPriority priority, string source, string userId, string userName, string userEmail, string takenBy)
    {
        var wanted = (requested ?? new()).Where(l => !string.IsNullOrWhiteSpace(l.ItemId)).ToList();
        if (wanted.Count == 0) throw new AppException("Choose at least one product to order.");
        if (wanted.Count > MaxLines) throw new AppException($"An order can have up to {MaxLines} products. Split it into two orders.");
        if (wanted.Any(l => !(l.Quantity > 0))) throw new AppException("Every quantity must be more than 0.");
        if (wanted.Any(l => l.Quantity > MaxQuantity)) throw new AppException($"A quantity can't be more than {MaxQuantity:0}.");

        var note = (noteIn ?? "").Trim();
        if (note.Length > 500) throw new AppException("The note can be up to 500 characters.");

        var city = (cityIn ?? "").Trim();
        if (client.Cities.Count > 0)
            city = city.Length == 0 ? client.Cities[0]
                : client.Cities.FirstOrDefault(c => c.Equals(city, StringComparison.OrdinalIgnoreCase)) ?? throw new AppException("Choose one of your cities.");
        else city = "";

        var ids = wanted.Select(l => l.ItemId).Distinct().ToList();
        var found = (await db.Items.Find(i => i.BusinessId == businessId && i.IsActive && ids.Contains(i.Id)).ToListAsync()).ToDictionary(i => i.Id);
        if (found.Count != ids.Count) throw new AppException("One of those products is no longer available. Refresh the page and try again.");

        var lines = wanted.GroupBy(l => l.ItemId).Select(g =>
        {
            var i = found[g.Key];
            return new OrderLine
            {
                ItemId = i.Id, Label = Label(i), Name = i.Name, Variant = i.Variant, Cloth = i.Cloth, Colour = i.Colour, Size = i.Size,
                Unit = i.Unit, Quantity = g.Sum(x => x.Quantity),
            };
        }).ToList();
        if (lines.Any(l => l.Quantity > MaxQuantity)) throw new AppException($"A quantity can't be more than {MaxQuantity:0}.");

        var seq = await numbers.NextOrderSeqAsync(businessId);
        var order = new Order
        {
            BusinessId = businessId, Seq = seq, OrderNumber = OrderNumber(seq), ClientId = client.Id, ClientName = client.Name, City = city,
            UserId = userId, UserName = userName, UserEmail = userEmail, Note = note, Lines = lines,
            Priority = priority, PriorityRank = (int)priority, Source = source, TakenBy = takenBy,
        };
        if (source == OrderSource.Phone) { order.Status = OrderStatus.Accepted; order.HandledBy = takenBy; order.HandledAt = DateTime.UtcNow; }
        await db.Orders.InsertOneAsync(order);
        return order;
    }

    public async Task<List<Order>> ForClientAsync(string businessId, string clientId) =>
        await db.Orders.Find(o => o.BusinessId == businessId && o.ClientId == clientId).SortByDescending(o => o.CreatedAt).Limit(200).ToListAsync();

    public async Task<Order> GetForClientAsync(string businessId, string clientId, string id) =>
        await db.Orders.Find(o => o.BusinessId == businessId && o.ClientId == clientId && o.Id == id).FirstOrDefaultAsync()
        ?? throw new NotFoundException("Order");

    /// <summary>A client can cancel their own order only while it is still new.</summary>
    public async Task<Order> CancelByClientAsync(string businessId, string clientId, string id)
    {
        var o = await GetForClientAsync(businessId, clientId, id);
        if (o.Status != OrderStatus.New) throw new AppException("This order has already been picked up, so it can't be cancelled here. Please contact us.");
        return await SetAsync(o, OrderStatus.Cancelled, "Cancelled by the client", "Client");
    }

    // ---------------------------------------------------------------- owner and staff side
    public async Task<PagedResult<Order>> ListAsync(string businessId, OrderQuery q)
    {
        var f = Builders<Order>.Filter;
        var filter = f.Eq(o => o.BusinessId, businessId);
        if (q.Status.HasValue) filter &= f.Eq(o => o.Status, q.Status.Value);
        if (!string.IsNullOrWhiteSpace(q.ClientId)) filter &= f.Eq(o => o.ClientId, q.ClientId);
        if (q.From.HasValue) filter &= f.Gte(o => o.CreatedAt, DateTime.SpecifyKind(q.From.Value.Date, DateTimeKind.Utc).AddHours(-5.5));   // dates are Indian days
        if (q.To.HasValue) filter &= f.Lt(o => o.CreatedAt, DateTime.SpecifyKind(q.To.Value.Date, DateTimeKind.Utc).AddDays(1).AddHours(-5.5));
        var page = Math.Max(q.Page, 1);
        var size = Math.Clamp(q.PageSize, 10, 100);
        var total = await db.Orders.CountDocumentsAsync(filter);
        // Open orders (waiting or accepted): urgent first, then newest. Finished ones: newest first.
        var open = q.Status is OrderStatus.New or OrderStatus.Accepted;
        var sort = open ? Builders<Order>.Sort.Descending(o => o.PriorityRank).Descending(o => o.CreatedAt) : Builders<Order>.Sort.Descending(o => o.CreatedAt);
        var items = await db.Orders.Find(filter).Sort(sort).Skip((page - 1) * size).Limit(size).ToListAsync();
        return new PagedResult<Order>(items, total, page, size);
    }

    public async Task<Order> GetAsync(string businessId, string id) =>
        await db.Orders.Find(o => o.BusinessId == businessId && o.Id == id).FirstOrDefaultAsync() ?? throw new NotFoundException("Order");

    /// <summary>Orders still New that were placed in the last 10 minutes: what the open screens ring for.
    /// The browser remembers which ones it has already rung for, so asking again is harmless.</summary>
    public async Task<List<Order>> RecentNewAsync(string businessId)
    {
        var since = DateTime.UtcNow.AddMinutes(-10);
        return await db.Orders.Find(o => o.BusinessId == businessId && o.Status == OrderStatus.New && o.Source != OrderSource.Phone && o.CreatedAt > since)
            .SortBy(o => o.CreatedAt).Limit(10).ToListAsync();
    }

    public Task<long> CountNewAsync(string businessId) =>
        db.Orders.CountDocumentsAsync(o => o.BusinessId == businessId && o.Status == OrderStatus.New);

    /// <summary>New -> Accepted, or New/Accepted -> Cancelled. (Billed happens by making a bill from the order.)</summary>
    public async Task<Order> SetStatusAsync(string businessId, string id, OrderStatus status, string? reason, string by)
    {
        var o = await GetAsync(businessId, id);
        if (o.Status is OrderStatus.Billed or OrderStatus.Cancelled)
            throw new AppException($"This order is already {o.Status.ToString().ToLowerInvariant()}.");
        if (status == OrderStatus.Accepted)
        {
            if (o.Status != OrderStatus.New) throw new AppException("This order is already accepted.");
            return await SetAsync(o, OrderStatus.Accepted, "", by);
        }
        if (status == OrderStatus.Cancelled)
            return await SetAsync(o, OrderStatus.Cancelled, string.IsNullOrWhiteSpace(reason) ? "Cancelled by us" : reason.Trim(), by);
        throw new AppException("Make a bill from the order to mark it as billed.");
    }

    /// <summary>The order a new bill is being made from: it must be this client's and still open.</summary>
    public async Task<Order> ForBillingAsync(string businessId, string orderId, string clientId)
    {
        var o = await GetAsync(businessId, orderId);
        if (o.ClientId != clientId) throw new AppException("That order belongs to a different client.");
        if (o.Status is OrderStatus.Billed or OrderStatus.Cancelled) throw new AppException($"That order is already {o.Status.ToString().ToLowerInvariant()}.");
        return o;
    }

    public async Task MarkBilledAsync(string businessId, string orderId, Invoice inv) =>
        await db.Orders.UpdateOneAsync(o => o.Id == orderId && o.BusinessId == businessId, Builders<Order>.Update
            .Set(o => o.Status, OrderStatus.Billed).Set(o => o.InvoiceId, inv.Id).Set(o => o.InvoiceNumber, inv.InvoiceNumber)
            .Set(o => o.HandledAt, DateTime.UtcNow).Set(o => o.UpdatedAt, DateTime.UtcNow));

    private async Task<Order> SetAsync(Order o, OrderStatus status, string reason, string by)
    {
        o.Status = status; o.CancelReason = status == OrderStatus.Cancelled ? reason : ""; o.HandledBy = by; o.HandledAt = DateTime.UtcNow; o.UpdatedAt = DateTime.UtcNow;
        await db.Orders.ReplaceOneAsync(x => x.Id == o.Id && x.BusinessId == o.BusinessId, o);
        return o;
    }
}
