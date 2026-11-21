using Content.Shared.Examine;

namespace Content.Shared._Stories.CorpLabel;

public sealed class STCorpLabelSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCorpLabelComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<STCorpLabelComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(STCorpLabelComponent)))
        {
            args.PushMarkup(Loc.GetString("st-corp-label-examine",
                ("manufacturer", Loc.GetString(ent.Comp.Manufacturer))));
        }
    }
}
