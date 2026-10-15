using Content.Shared._Stories.Synth.VoiceSynthesizer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Synth.VoiceSynthesizer;

[UsedImplicitly]
public sealed class STSynthVoiceSynthesizerBui : BoundUserInterface
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private STSynthVoiceSynthesizerWindow? _window;

    public STSynthVoiceSynthesizerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<STSynthVoiceSynthesizerWindow>();
        _window.Populate(_prototype, _compFactory);
        _window.OnLinePressed += OnLinePressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not STSynthVoiceBuiState buiState)
            return;

        _window.SetCooldown(buiState.OnCooldown, buiState.RemainingCooldown);
        _window.SetTheme(buiState.UseAlternateSound);
    }

    private void OnLinePressed(string lineId)
    {
        SendMessage(new STSynthVoicePlayLineMsg(lineId));
    }
}
