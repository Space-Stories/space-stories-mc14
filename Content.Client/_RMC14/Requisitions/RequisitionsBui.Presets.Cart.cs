using System.Linq;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsBui
{
    private RequisitionPreset? CreatePresetFromCart()
    {
        if (_window == null || _state == null || _cart.Count is < 1 or > RequisitionPresetStore.MaxLines ||
            _cart.Count > _state.MaxRequestLines)
            return null;
        var reason = _window.RequestReason.Text.Trim();
        if (reason.Length > Math.Min(RequisitionPresetStore.MaxReasonLength, _state.MaxRequestReasonLength))
            return null;
        var items = new List<RequisitionPresetItem>(_cart.Count);
        var total = 0L;
        foreach (var (key, amount) in _cart)
        {
            if (!TryGetEntry(key, out var entry) || entry.BlackMarket || entry.BlackMarketCost != 0 ||
                entry.Cost < 0 || amount <= 0 || amount > _state.MaxRequestItems)
                return null;
            total += (long) entry.Cost * amount;
            items.Add(new RequisitionPresetItem
            {
                Key = RequisitionPresetStore.GetKey(entry),
                Name = GetDisplayText(entry).Name,
                Amount = amount,
            });
        }
        if (total > int.MaxValue)
            return null;
        return new RequisitionPreset
        {
            Name = _window.PresetsTab.PresetName.Text.Trim(),
            Reason = reason,
            Items = items,
        };
    }

    private static bool SamePresetContents(RequisitionPreset first, RequisitionPreset second)
    {
        return first.Reason.Trim() == second.Reason.Trim() && first.Items.Count == second.Items.Count &&
               first.Items.All(item => second.Items.Any(other =>
                   other.Key == item.Key && other.Amount == item.Amount));
    }

    private bool CartMatches(Dictionary<CartKey, int> cart, string reason)
    {
        return _window != null && _window.RequestReason.Text.Trim() == reason.Trim() && cart.Count == _cart.Count &&
               cart.All(item => _cart.TryGetValue(item.Key, out var amount) && amount == item.Value);
    }
}
