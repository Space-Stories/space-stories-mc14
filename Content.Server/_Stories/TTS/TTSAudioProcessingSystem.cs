using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Robust.Shared.Configuration;

namespace Content.Server._Stories.TTS;

public sealed class TtsAudioProcessingSystem : EntitySystem
{
    private const TTSAudioEffect KnownEffects =
        TTSAudioEffect.Hunter |
        TTSAudioEffect.XenoHivemind |
        TTSAudioEffect.Ares |
        TTSAudioEffect.StandardRadio |
        TTSAudioEffect.Megaphone;

    private static readonly TTSAudioEffect[] EffectOrder =
    {
        TTSAudioEffect.Hunter,
        TTSAudioEffect.XenoHivemind,
        TTSAudioEffect.Ares,
        TTSAudioEffect.StandardRadio,
        TTSAudioEffect.Megaphone,
    };

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly HashSet<TTSAudioEffect> _disabledEffects = new();
    private readonly HashSet<TTSAudioEffect> _disabledEffectCombinations = new();
    private readonly object _disabledEffectsLock = new();

    private string _ffmpegPath = "ffmpeg";
    private string _standardRadioFfmpegFilter = "";
    private string _xenoFfmpegFilter = "";
    private string _hunterFfmpegFilter = "";
    private string _megaphoneFfmpegFilter = "";
    private string _aresFfmpegFilter = "";
    private bool _radioEffectEnabled;
    private bool _processingDisabled;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("tts.processing");

        _cfg.OnValueChanged(SCCVars.TTSRadioEffect, v =>
        {
            _radioEffectEnabled = v;
            if (v)
                EnableEffect(TTSAudioEffect.StandardRadio);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSFfmpegPath, v =>
        {
            _ffmpegPath = v;
            EnableAllEffects();
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSStandardRadioFfmpegFilter, v =>
        {
            _standardRadioFfmpegFilter = v;
            EnableEffect(TTSAudioEffect.StandardRadio);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSXenoFfmpegFilter, v =>
        {
            _xenoFfmpegFilter = v;
            EnableEffect(TTSAudioEffect.XenoHivemind);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSHunterFfmpegFilter, v =>
        {
            _hunterFfmpegFilter = v;
            EnableEffect(TTSAudioEffect.Hunter);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSMegaphoneFfmpegFilter, v =>
        {
            _megaphoneFfmpegFilter = v;
            EnableEffect(TTSAudioEffect.Megaphone);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSAresFfmpegFilter, v =>
        {
            _aresFfmpegFilter = v;
            EnableEffect(TTSAudioEffect.Ares);
        }, true);
    }

    public async Task<byte[]> ProcessRadioAudio(EntityUid uid, byte[] audioData)
    {
        var effects = TTSAudioEffect.StandardRadio;

        if (_entityManager.HasComponent<HunterComponent>(uid))
            effects |= TTSAudioEffect.Hunter;

        if (_entityManager.HasComponent<XenoComponent>(uid))
            effects |= TTSAudioEffect.XenoHivemind;

        return await ApplyPlaybackEffects(audioData, effects);
    }

    public async Task<byte[]> ApplyPlaybackEffects(byte[] oggData, TTSAudioEffect effects)
    {
        effects = NormalizeEffects(effects, _radioEffectEnabled);

        if (effects == TTSAudioEffect.None || IsProcessingDisabled())
            return oggData;

        var filterChain = BuildFilterChain(effects, GetEffectFilter, IsEffectDisabled);
        foreach (var effect in EnumerateEffects(filterChain.MissingEffects))
        {
            DisableEffect(
                effect,
                $"FFmpeg filter for {GetEffectName(effect)} is not configured. Skipping this effect.");
        }

        if (filterChain.ActiveEffects == TTSAudioEffect.None ||
            IsEffectCombinationDisabled(filterChain.ActiveEffects))
            return oggData;

        if (string.IsNullOrWhiteSpace(_ffmpegPath))
        {
            DisableProcessing("FFmpeg path is not configured.");
            return oggData;
        }

        return await ApplyEffectChain(oggData, filterChain.Filter, filterChain.ActiveEffects);
    }

    internal static TTSAudioEffect NormalizeEffects(TTSAudioEffect effects, bool radioEffectEnabled)
    {
        effects &= KnownEffects;

        if (!radioEffectEnabled)
            effects &= ~TTSAudioEffect.StandardRadio;

        // Hivemind speech is not transmitted through a physical radio channel.
        if ((effects & TTSAudioEffect.XenoHivemind) != 0)
            effects &= ~TTSAudioEffect.StandardRadio;

        return effects;
    }

    internal static IEnumerable<TTSAudioEffect> EnumerateEffects(TTSAudioEffect effects)
    {
        foreach (var effect in EffectOrder)
        {
            if ((effects & effect) != 0)
                yield return effect;
        }
    }

    internal static TtsAudioFilterChain BuildFilterChain(
        TTSAudioEffect effects,
        Func<TTSAudioEffect, string> getFilter,
        Func<TTSAudioEffect, bool> isDisabled)
    {
        var filters = new List<string>();
        var activeEffects = TTSAudioEffect.None;
        var missingEffects = TTSAudioEffect.None;

        foreach (var effect in EnumerateEffects(effects))
        {
            if (isDisabled(effect))
                continue;

            var filter = getFilter(effect);
            if (string.IsNullOrWhiteSpace(filter))
            {
                missingEffects |= effect;
                continue;
            }

            filters.Add(filter.Trim().Trim(','));
            activeEffects |= effect;
        }

        return new TtsAudioFilterChain(
            string.Join(",", filters),
            activeEffects,
            missingEffects);
    }

    private string GetEffectFilter(TTSAudioEffect effect)
    {
        return effect switch
        {
            TTSAudioEffect.Hunter => _hunterFfmpegFilter,
            TTSAudioEffect.XenoHivemind => _xenoFfmpegFilter,
            TTSAudioEffect.Ares => _aresFfmpegFilter,
            TTSAudioEffect.StandardRadio => _standardRadioFfmpegFilter,
            TTSAudioEffect.Megaphone => _megaphoneFfmpegFilter,
            _ => "",
        };
    }

    private static string GetEffectName(TTSAudioEffect effect)
    {
        return effect switch
        {
            TTSAudioEffect.Hunter => "hunter",
            TTSAudioEffect.XenoHivemind => "xeno hivemind",
            TTSAudioEffect.Ares => "ARES",
            TTSAudioEffect.StandardRadio => "standard radio",
            TTSAudioEffect.Megaphone => "megaphone",
            _ => effect.ToString(),
        };
    }

    private static string GetEffectCombinationName(TTSAudioEffect effects)
    {
        var names = new List<string>();

        foreach (var effect in EnumerateEffects(effects))
        {
            names.Add(GetEffectName(effect));
        }

        return string.Join(" -> ", names);
    }

    private async Task<byte[]> ApplyEffectChain(
        byte[] oggData,
        string filterChain,
        TTSAudioEffect effects)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        processStartInfo.ArgumentList.Add("-v");
        processStartInfo.ArgumentList.Add("error");
        processStartInfo.ArgumentList.Add("-i");
        processStartInfo.ArgumentList.Add("pipe:0");
        processStartInfo.ArgumentList.Add("-filter:a");
        processStartInfo.ArgumentList.Add(filterChain);
        processStartInfo.ArgumentList.Add("-f");
        processStartInfo.ArgumentList.Add("ogg");
        processStartInfo.ArgumentList.Add("pipe:1");

        var effectNames = GetEffectCombinationName(effects);

        try
        {
            using var process = new Process { StartInfo = processStartInfo };

            if (!process.Start())
            {
                DisableProcessing($"Failed to start FFmpeg for TTS effects: {effectNames}.");
                return oggData;
            }

            using var memoryStream = new MemoryStream();
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.StandardInput.BaseStream.WriteAsync(oggData, 0, oggData.Length);
            process.StandardInput.Close();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            var errorOutput = await errorTask;
            if (process.ExitCode != 0)
            {
                DisableEffectCombination(
                    effects,
                    $"FFmpeg for TTS effects {effectNames} exited with code {process.ExitCode}. Stderr: {errorOutput}",
                    true);
                return oggData;
            }

            var processedData = memoryStream.ToArray();
            if (processedData.Length == 0)
            {
                DisableEffectCombination(
                    effects,
                    $"FFmpeg for TTS effects {effectNames} produced an empty output. Stderr: {errorOutput}.",
                    false);
                return oggData;
            }

            return processedData;
        }
        catch (Win32Exception)
        {
            DisableProcessing($"FFmpeg not found at path '{_ffmpegPath}'.");
            return oggData;
        }
        catch (Exception e)
        {
            DisableEffectCombination(
                effects,
                $"An exception occurred while running FFmpeg for TTS effects {effectNames}: {e}",
                true);
            return oggData;
        }
    }

    private bool IsProcessingDisabled()
    {
        lock (_disabledEffectsLock)
        {
            return _processingDisabled;
        }
    }

    private bool IsEffectDisabled(TTSAudioEffect effect)
    {
        lock (_disabledEffectsLock)
        {
            return _disabledEffects.Contains(effect);
        }
    }

    private bool IsEffectCombinationDisabled(TTSAudioEffect effects)
    {
        lock (_disabledEffectsLock)
        {
            return _disabledEffectCombinations.Contains(effects);
        }
    }

    private void DisableProcessing(string reason)
    {
        lock (_disabledEffectsLock)
        {
            if (_processingDisabled)
                return;

            _processingDisabled = true;
        }

        _sawmill.Error($"{reason} Disabling TTS audio processing until the FFmpeg path changes.");
    }

    private void DisableEffect(TTSAudioEffect effect, string reason)
    {
        lock (_disabledEffectsLock)
        {
            if (!_disabledEffects.Add(effect))
                return;
        }

        _sawmill.Warning($"{reason} It will be retried when its audio filter configuration changes.");
    }

    private void DisableEffectCombination(TTSAudioEffect effects, string reason, bool error)
    {
        lock (_disabledEffectsLock)
        {
            if (!_disabledEffectCombinations.Add(effects))
                return;
        }

        var message =
            $"{reason} Disabling this TTS effect combination until one of its audio filters changes.";
        if (error)
            _sawmill.Error(message);
        else
            _sawmill.Warning(message);
    }

    private void EnableEffect(TTSAudioEffect effect)
    {
        lock (_disabledEffectsLock)
        {
            _disabledEffects.Remove(effect);
            _disabledEffectCombinations.RemoveWhere(effects => (effects & effect) != 0);
        }
    }

    private void EnableAllEffects()
    {
        lock (_disabledEffectsLock)
        {
            _processingDisabled = false;
            _disabledEffects.Clear();
            _disabledEffectCombinations.Clear();
        }
    }
}

internal readonly record struct TtsAudioFilterChain(
    string Filter,
    TTSAudioEffect ActiveEffects,
    TTSAudioEffect MissingEffects);
