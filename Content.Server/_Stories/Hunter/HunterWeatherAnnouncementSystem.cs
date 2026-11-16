using Content.Server.Chat.Managers;
using Content.Shared._RMC14.Weather;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Hunter;

public sealed class HunterWeatherAnnouncementSystem : EntitySystem
{
    private static readonly SoundSpecifier ElderOverseerSound =
        new SoundCollectionSpecifier("STHunterElderOverseer", AudioParams.Default.WithVolume(-4f));

    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCWeatherFactionWarningEvent>(OnWeatherFactionWarning);
    }

    private void OnWeatherFactionWarning(ref RMCWeatherFactionWarningEvent ev)
    {
        var message = Loc.GetString("st-hunter-weather-warning", ("weather", ev.WeatherName));
        var title = Loc.GetString("st-hunter-weather-warning-title");
        var filter = Filter.Empty().AddWhereAttachedEntity(IsLivingHunter);

        var wrappedMessage = $"[bold][font size=16][color=#af0614]{FormattedMessage.EscapeText(title)}[/color][/font][/bold]\n\n" +
                             $"[bold][color=#af0614]{FormattedMessage.EscapeText(message)}[/color][/bold]";

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, default, false, true, null);
        _audio.PlayGlobal(ElderOverseerSound, filter, true);
    }

    private bool IsLivingHunter(EntityUid uid)
    {
        return HasComp<HunterComponent>(uid) &&
               TryComp(uid, out MobStateComponent? mobState) &&
               _mobState.IsAlive(uid, mobState);
    }
}
