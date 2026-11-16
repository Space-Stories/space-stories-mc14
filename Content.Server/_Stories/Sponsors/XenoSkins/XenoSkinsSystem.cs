using System.Linq;
using Content.Server.Mind;
using Content.Shared._Stories.Sponsors.XenoSkins;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Jittering;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Server._Stories.Sponsors.XenoSkins;

public sealed class XenoSkinsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SponsorsManager _partners = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoSkinsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<XenoSkinsComponent, MindAddedMessage>(OnMapInit);
        SubscribeLocalEvent<XenoSkinsComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<XenoSkinsComponent, XenoOpenSkinsMenuActionEvent>(OnXenoSkinsMenuAction);
        SubscribeLocalEvent<XenoSkinsComponent, XenoSkinsDoAfterEvent>(OnXenoSkinsDoAfter);

        Subs.BuiEvents<XenoSkinsComponent>(XenoSkinsUIKey.Key,
            subs =>
            {
                subs.Event<XenoSkinsBuiMsg>(OnXenoSkinsBui);
            });
    }

    private void OnMapInit<T>(Entity<XenoSkinsComponent> xeno, ref T args)
    {
        if (!_mind.TryGetMind(xeno, out var mindId, out var mind) || mind == null || mind.UserId == null)
            return;

        if (_partners.TryGetInfo(mind.UserId.Value, out var sponsorData))
        {
            xeno.Comp.Skins = sponsorData.XenoSkins
                .Select(id => new ProtoId<XenoSkinsPrototype>(id))
                .ToList();
        }

        if (xeno.Comp.Skins.Count > 0)
            xeno.Comp.ActionEntity = _actions.AddAction(xeno, xeno.Comp.Action);

        Dirty(xeno);
    }

    private void OnComponentShutdown(Entity<XenoSkinsComponent> xeno, ref ComponentShutdown args)
    {
        _actions.RemoveAction(xeno.Owner, xeno.Comp.ActionEntity);
    }

    private void OnXenoSkinsMenuAction(Entity<XenoSkinsComponent> xeno, ref XenoOpenSkinsMenuActionEvent args)
    {
        if (xeno.Comp.ActiveDoAfter != null)
        {
            _doAfter.Cancel(xeno.Comp.ActiveDoAfter.Value);
            xeno.Comp.ActiveDoAfter = null;
            _popup.PopupClient(Loc.GetString("stories-xeno-skin-apply-cancel"), xeno, xeno);
            return;
        }

        _ui.OpenUi(xeno.Owner, XenoSkinsUIKey.Key, xeno);
    }

    private void OnXenoSkinsBui(Entity<XenoSkinsComponent> xeno, ref XenoSkinsBuiMsg args)
    {
        if (args.Actor != xeno.Owner)
            return;

        var actor = args.Actor;
        var skinIdString = args.Choice;

        _ui.CloseUi(xeno.Owner, XenoSkinsUIKey.Key, actor);

        var skinProtoId = new ProtoId<XenoSkinsPrototype>(skinIdString);

        if (!_prototype.TryIndex(skinProtoId, out XenoSkinsPrototype? skinIndex) ||
            !xeno.Comp.Skins.Contains(skinProtoId))
            return;

        var path = SpriteSpecifierSerializer.TextureRoot / skinIndex.SpriteRsi;
        var ev = new XenoSkinsDoAfterEvent(path, skinIdString);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.DoAfterDelay, ev, xeno);

        if (xeno.Comp.DoAfterDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("stories-xeno-skins-apply-start-self"), xeno, xeno);

        if (_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            xeno.Comp.ActiveDoAfter = id;

            _jitter.DoJitter(xeno, xeno.Comp.DoAfterDelay, true, 80, 8, true);

            var popupOthers = Loc.GetString("stories-xeno-skins-apply-start-others", ("xeno", xeno));
            _popup.PopupEntity(popupOthers, xeno, Filter.PvsExcept(xeno), true, PopupType.Medium);

            var popupSelf = Loc.GetString("stories-xeno-skins-apply-start-self");
            _popup.PopupEntity(popupSelf, xeno, xeno, PopupType.Medium);
        }
    }

    private void OnXenoSkinsDoAfter(Entity<XenoSkinsComponent> xeno, ref XenoSkinsDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            xeno.Comp.ActiveDoAfter = null;
            return;
        }

        xeno.Comp.CurrentSkin = new ProtoId<XenoSkinsPrototype>(args.Proto);
        xeno.Comp.ActiveDoAfter = null;
        Dirty(xeno);

        RaiseNetworkEvent(new XenoSkinChangeRSIEvent(GetNetEntity(xeno), args.Path), xeno);
        _actions.RemoveAction(xeno.Owner, xeno.Comp.ActionEntity);
    }
}
