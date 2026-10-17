namespace Content.Shared._RMC14.MilitaryPolice;

/// <summary>
/// Only an original report carries the structured data needed to import it into JAS.
/// </summary>
[RegisterComponent]
public sealed partial class RMCIncidentReportPaperComponent : Component
{
    [DataField]
    public RMCIncidentReport? Report;
}
