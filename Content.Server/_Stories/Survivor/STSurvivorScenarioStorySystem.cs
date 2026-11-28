using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server.Chat.Managers;
using Content.Shared._Stories.Survivor;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Stories.Survivor;

public sealed class STSurvivorScenarioStorySystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distressSignal = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STSurvivorScenarioStoryComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(Entity<STSurvivorScenarioStoryComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (_distressSignal.ActiveNightmareScenario is not { Length: > 0 } scenario)
            return;

        if (!Loc.TryGetString($"{ent.Comp.LocPrefix}-{scenario}", out var story))
            return;

        _chat.ChatMessageToOne(ChatChannel.Server, story, story, default, false, args.Player.Channel);

        if (ent.Comp.NoDamageScenarios.Contains(scenario) || ent.Comp.DamageHits <= 0)
            return;

        var blunt = _prototypes.Index(BluntDamage);
        for (var i = 0; i < ent.Comp.DamageHits; i++)
        {
            var damage = new DamageSpecifier(blunt, _random.NextFloat(ent.Comp.DamageMin, ent.Comp.DamageMax));
            _damageable.TryChangeDamage(ent, damage, true, false);
        }
    }
}
