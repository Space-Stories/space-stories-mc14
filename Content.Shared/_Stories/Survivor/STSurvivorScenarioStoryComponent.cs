namespace Content.Shared._Stories.Survivor;

[RegisterComponent]
public sealed partial class STSurvivorScenarioStoryComponent : Component
{
    [DataField(required: true)]
    public string LocPrefix = string.Empty;

    [DataField]
    public float DamageMin = 3;

    [DataField]
    public float DamageMax = 10;

    [DataField]
    public int DamageHits = 3;

    [DataField]
    public List<string> NoDamageScenarios = new();
}
