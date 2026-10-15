// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Dropship;

public sealed partial class DropshipComponent
{
    // CM13 uses three shakes, reduced to one for buckled passengers, on takeoff and landing.
    [DataField]
    public int STPassengerCameraShakes = 3;

    [DataField]
    public int STBuckledPassengerCameraShakes = 1;

    [DataField]
    public int STPassengerCameraShakeStrength = 1;
}
