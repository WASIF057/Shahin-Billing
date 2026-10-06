namespace ShahinBilling.Api.Dtos;

/// <summary>Status: Ok (will be / was imported), Skipped (already exists) or Error (fix the row and try again).</summary>
public record ImportRow(int Row, string Status, string Message, string Summary);
public record ImportResult(int Total, int Ok, int Skipped, int Errors, bool Imported, IReadOnlyList<ImportRow> Rows);
