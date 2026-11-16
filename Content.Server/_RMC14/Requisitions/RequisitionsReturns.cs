namespace Content.Server._RMC14.Requisitions;

/// <summary>
/// Divides a percentage of paid requisitions between container and paperwork returns, rounding down.
/// </summary>
public static class RequisitionsReturns
{
    public static (int Crate, int Invoice) Calculate(
        int paid, double percent, int crateShare, int invoiceShare)
    {
        if (paid <= 0 || !double.IsFinite(percent))
            return (0, 0);

        crateShare = Math.Max(0, crateShare);
        invoiceShare = Math.Max(0, invoiceShare);
        var shares = (long) crateShare + invoiceShare;
        if (shares == 0)
            return (0, 0);

        var budget = Math.Min(paid * (decimal) Math.Clamp(percent, 0, 100) / 100, paid - 1m);
        return ((int) (budget * crateShare / shares), (int) (budget * invoiceShare / shares));
    }
}
