using System.Linq;
using Content.Client._Stories.Lobby.UI;
using Content.Client._Stories.Sponsors;
using Content.Client._Stories.TTS;
using Content.Shared._Stories.TTS;
using Content.Shared.Humanoid;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = new();
    private TTSVoiceSelectionWindow? _ttsWindow;

    private void InitializeVoice()
    {
        VoiceButton.OnPressed += _ =>
        {
            if (_ttsWindow != null && _ttsWindow.IsOpen)
            {
                _ttsWindow.MoveToFront();
                return;
            }

            var isSponsor = _entManager.System<SponsorsSystem>().TryGetInfo(out var info) && info.Tier > 0;
            var voices = new List<(TTSVoicePrototype Voice, bool Unlocked)>();
            foreach (var v in _prototypeManager.EnumeratePrototypes<TTSVoicePrototype>().Where(o => o.RoundStart))
            {
                if (Profile != null)
                {
                    if (v.Blacklist != null && v.Blacklist.Contains(Profile.Species))
                        continue;

                    if (v.Sex != Sex.Unsexed && v.Sex != Profile.Sex)
                        continue;
                }

                bool unlocked = !v.SponsorOnly || (isSponsor && info!.AllowedTTSVoices.Contains(v.ID));
                voices.Add((v, unlocked));
            }

            _ttsWindow = new TTSVoiceSelectionWindow(voices, Profile?.Voice);
            _ttsWindow.OnVoiceSelected += voiceId =>
            {
                var idx = _voiceList.FindIndex(v => v.ID == voiceId);
                if (idx != -1)
                {
                    VoiceButton.Text = Loc.GetString(_voiceList[idx].Name);
                    SetVoice(voiceId);
                }
            };
            _ttsWindow.OnPreviewPlay += voiceId =>
            {
                var isHunter = Profile?.Species == "STHunter";
                _entManager.System<TTSSystem>().RequestPreviewTTS(voiceId, isHunter);
            };
            _ttsWindow.OpenCentered();
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        var isHunter = Profile.Species == "STHunter";
        var isSponsor = _entManager.System<SponsorsSystem>().TryGetInfo(out var info) && info.Tier > 0;

        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(o => o.RoundStart)
            .Where(o => !o.SponsorOnly || (isSponsor && info!.AllowedTTSVoices.Contains(o.ID)))
            .Where(o => o.Blacklist == null || !o.Blacklist.Contains(Profile.Species))
            .Where(o => o.Sex == Sex.Unsexed || o.Sex == Profile.Sex)
            .ToList();

        var voice = _voiceList.FirstOrDefault(x => x.ID == Profile.Voice) ?? _voiceList.FirstOrDefault();
        if (voice != null)
        {
            VoiceButton.Text = Loc.GetString(voice.Name);
            SetVoice(voice.ID);
        }
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;

        var isHunter = Profile.Species == "STHunter";
        _entManager.System<TTSSystem>().RequestPreviewTTS(Profile.Voice, isHunter);
    }
}
