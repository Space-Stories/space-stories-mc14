using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared._Stories.ZLevels.Ghost;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server._Stories.ZLevels.Ghost;

public sealed partial class ZLevelGhostMoverSystem : SharedZLevelGhostMoverSystem
{
    [Dependency] private readonly SharedZLevelsSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ZLevelGhostMoveMessage>(OnGhostMoveMessage);
    }

    private void OnGhostMoveMessage(ZLevelGhostMoveMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } playerEnt)
            return;

        if (!HasComp<GhostComponent>(playerEnt) || !HasComp<ZLevelGhostMoverComponent>(playerEnt))
            return;

        if (msg.Up)
            _zLevel.TryMoveUp(playerEnt);
        else
            _zLevel.TryMoveDown(playerEnt);
    }
}
