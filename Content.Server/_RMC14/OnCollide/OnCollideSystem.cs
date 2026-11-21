using Content.Server.Chat.Systems;
using Content.Server._RMC14.Emote;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Synth; // Stories-SynthNoPainEmote
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.OnCollide;

public sealed class OnCollideSystem : SharedOnCollideSystem
{
    [Dependency] private readonly RMCEmoteSystem _rmcEmotes = default!;

    protected override void DoEmote(Entity<DamageOnCollideComponent> ent, EntityUid other)
    {
        if (ent.Comp.Emote is not { } emote)
            return;

        // Stories-SynthNoPainEmote-Start
        if (HasComp<SynthComponent>(other))
            return;
        // Stories-SynthNoPainEmote-End


        if (HasComp<XenoComponent>(other) && ent.Comp.XenoEmote != null)
        {
            emote = ent.Comp.XenoEmote.Value;
        }
        else if (HasComp<XenoComponent>(other))
            return;

        _rmcEmotes.TryEmoteWithChat(other, emote);
    }
}
