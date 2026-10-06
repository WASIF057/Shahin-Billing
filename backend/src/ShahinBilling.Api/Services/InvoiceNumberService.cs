using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Business rule 4.4: atomic per-business counter. One running number, never reset.</summary>
public class InvoiceNumberService(MongoContext db)
{
    // GST invoices count on the business id (unchanged); Non-GST bills have their own counter
    private static string CounterId(string businessId, bool nonGst) => nonGst ? businessId + ":nongst" : businessId;

    public async Task<int> NextSeqAsync(string businessId, bool nonGst = false)
    {
        var id = CounterId(businessId, nonGst);
        var counter = await db.Counters.FindOneAndUpdateAsync(
            Builders<Counter>.Filter.Eq(c => c.Id, id),
            Builders<Counter>.Update.Inc(c => c.Seq, 1),
            new FindOneAndUpdateOptions<Counter> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
        return counter.Seq;
    }

    /// <summary>The next order number for this business (its own counter, so it never touches bill numbers).</summary>
    public async Task<int> NextOrderSeqAsync(string businessId)
    {
        var counter = await db.Counters.FindOneAndUpdateAsync(
            Builders<Counter>.Filter.Eq(c => c.Id, businessId + ":order"),
            Builders<Counter>.Update.Inc(c => c.Seq, 1),
            new FindOneAndUpdateOptions<Counter> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
        return counter.Seq;
    }

    /// <summary>The number the next saved bill will get. Only looks; it does not use the number up.</summary>
    public async Task<int> PeekNextSeqAsync(string businessId, bool nonGst = false)
    {
        var id = CounterId(businessId, nonGst);
        var counter = await db.Counters.Find(c => c.Id == id).FirstOrDefaultAsync();
        return (counter?.Seq ?? 0) + 1;
    }

    public static string Format(InvoiceNumbering n, int seq, bool nonGst = false)
    {
        var number = seq.ToString().PadLeft(Math.Clamp(n.Padding, 1, 8), '0');
        var prefix = nonGst ? n.NonGstPrefix : n.Prefix;
        var parts = new[] { prefix?.Trim(), number }.Where(p => !string.IsNullOrEmpty(p));
        return string.Join(n.Separator ?? "", parts);
    }
}
