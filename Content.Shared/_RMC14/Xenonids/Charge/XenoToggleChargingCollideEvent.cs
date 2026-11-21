namespace Content.Shared._RMC14.Xenonids.Charge;

[ByRefEvent]
public record struct XenoToggleChargingCollideEvent(Entity<ActiveXenoToggleChargingComponent> Charger, int Stage, bool Handled = false); // Stories-CrusherCharger
