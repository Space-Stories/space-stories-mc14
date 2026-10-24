using Robust.Shared.Configuration;

namespace Content.Shared._Stories.SCCVars;

/// <summary>
/// Stories modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class SCCVars
{
    /// <summary>
    /// Whether the TTS system is enabled on the server.
    /// </summary>
    public static readonly CVarDef<bool> TTSEnabled =
        CVarDef.Create("tts.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Whether the TTS system is enabled on the client.
    /// </summary>
    public static readonly CVarDef<bool> TTSEnabledClient =
        CVarDef.Create("tts.enabled_client", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// URL of the TTS server API.
    /// </summary>
    public static readonly CVarDef<string> TTSApiUrl =
        CVarDef.Create("tts.api_url", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Auth token of the TTS server API.
    /// </summary>
    public static readonly CVarDef<string> TTSApiToken =
        CVarDef.Create("tts.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Amount of seconds before timeout for API
    /// </summary>
    public static readonly CVarDef<int> TTSApiTimeout =
        CVarDef.Create("tts.api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Master volume setting of TTS sound
    /// </summary>
    public static readonly CVarDef<float> TTSVolumeMaster =
        CVarDef.Create("tts.volume_master", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound for marines
    /// </summary>
    public static readonly CVarDef<float> TTSVolumeMarines =
        CVarDef.Create("tts.volume_marines", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound for xenos
    /// </summary>
    public static readonly CVarDef<float> TTSVolumeXenos =
        CVarDef.Create("tts.volume_xenos", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound for others
    /// </summary>
    public static readonly CVarDef<float> TTSVolumeOther =
        CVarDef.Create("tts.volume_other", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound for radio
    /// </summary>
    public static readonly CVarDef<float> TTSVolumeRadio =
        CVarDef.Create("tts.volume_radio", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// TTS radio channels volume
    /// </summary>
    public static readonly CVarDef<string> TTSRadioVolumes =
        CVarDef.Create("tts.radio_volumes", "{}", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default volume setting of TTS sound for announcements
    /// </summary>
    public static readonly CVarDef<float> TTSVolumeAnnounce =
        CVarDef.Create("tts.volume_announce", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Count of in-memory cached tts voice lines.
    /// </summary>
    public static readonly CVarDef<int> TTSMaxCache =
        CVarDef.Create("tts.max_cache", 250, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Enable a radio effect for TTS messages sent over radio channels.
    /// </summary>
    public static readonly CVarDef<bool> TTSRadioEffect =
        CVarDef.Create("scc.tts.radio_effect_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// The path to the FFmpeg executable for audio processing.
    /// </summary>
    public static readonly CVarDef<string> TTSFfmpegPath =
        CVarDef.Create("scc.tts.ffmpeg_path", "", CVar.SERVERONLY);

    /// <summary>
    /// FFmpeg audio filter for standard radio TTS
    /// </summary>
    public static readonly CVarDef<string> TTSStandardRadioFfmpegFilter =
        CVarDef.Create("scc.tts.standard_radio_ffmpeg_filter",
            "highpass=f=1000,lowpass=f=500,acrusher=level_in=1:level_out=1:bits=4:mix=0.5:mode=log,loudnorm=I=-12:LRA=7",
            CVar.SERVERONLY);

    /// <summary>
    /// FFmpeg audio filter for Xenos
    /// </summary>
    public static readonly CVarDef<string> TTSXenoFfmpegFilter =
        CVarDef.Create("scc.tts.xeno_ffmpeg_filter",
            "highpass=f=250,lowpass=f=4000,vibrato=f=0.8:d=0.3,aecho=0.9:0.5:100|180:0.2|0.1,loudnorm=I=-20",
            CVar.SERVERONLY);

    /// <summary>
    /// FFmpeg audio filter for Hunters
    /// </summary>
    public static readonly CVarDef<string> TTSHunterFfmpegFilter =
        CVarDef.Create("scc.tts.hunter_ffmpeg_filter",
            "asetrate=44100*0.75,aresample=44100,lowpass=f=2500,equalizer=f=200:t=h:w=200:g=5,aecho=0.8:0.88:20:0.3,loudnorm=I=-15",
            CVar.SERVERONLY);

    /// <summary>
    /// FFmpeg audio filter for megaphones
    /// </summary>
    public static readonly CVarDef<string> TTSMegaphoneFfmpegFilter =
        CVarDef.Create("scc.tts.megaphone_ffmpeg_filter",
            "highpass=f=420,lowpass=f=4200,equalizer=f=900:t=q:w=1.1:g=4,equalizer=f=1800:t=q:w=1.0:g=6,equalizer=f=3200:t=q:w=1.2:g=4,acompressor=threshold=0.03:ratio=20:attack=0.1:release=150:makeup=16:knee=1:link=maximum:detection=peak,acrusher=level_in=1.4:level_out=0.9:bits=5:mix=0.45:mode=log,volume=30,alimiter=limit=0.98:attack=1:release=20:level=false",
            CVar.SERVERONLY);

    /// <summary>
    /// FFmpeg audio filter for ARES
    /// </summary>
    public static readonly CVarDef<string> TTSAresFfmpegFilter =
        CVarDef.Create("scc.tts.ares_ffmpeg_filter",
            "asetrate=44100*0.95,aresample=44100,highpass=f=150,lowpass=f=4000,acrusher=level_in=1:level_out=1:bits=5:mix=0.7:mode=log,tremolo=f=30:d=0.2,aecho=0.8:0.7:40:0.2,loudnorm=I=-12",
            CVar.SERVERONLY);

    /// <summary>
    /// Default TTS voice for ARES
    /// </summary>
    public static readonly CVarDef<string> TTSAresVoice =
        CVarDef.Create("scc.tts.ares_voice", "working_joe", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default TTS voice for High Command
    /// </summary>
    public static readonly CVarDef<string> TTSHighCommandVoice =
        CVarDef.Create("scc.tts.high_command_voice", "working_joe", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default TTS voice for Command
    /// </summary>
    public static readonly CVarDef<string> TTSCommandVoice =
        CVarDef.Create("scc.tts.command_voice", "working_joe", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Default TTS voice for Queen Mother
    /// </summary>
    public static readonly CVarDef<string> TTSQueenMotherVoice =
        CVarDef.Create("scc.tts.queen_mother_voice", "working_joe", CVar.SERVERONLY | CVar.ARCHIVE);

    /// Sponsors
    /// <summary>
    /// URL of the sponsors server API.
    /// </summary>
    public static readonly CVarDef<string> SponsorsApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);

    /*
     * Queue
     */

    /// <summary>
    /// Controls if the connections queue is enabled. If enabled stop kicking new players after `SoftMaxPlayers` cap and
    /// instead add them to queue.
    /// </summary>
    public static readonly CVarDef<bool> QueueEnabled =
        CVarDef.Create("queue.enabled", false, CVar.SERVERONLY);

    /*
     * Discord Auth
     */

    /// <summary>
    /// Enabled Discord linking, show linking button and modal window
    /// </summary>
    public static readonly CVarDef<bool> DiscordAuthEnabled =
        CVarDef.Create("discord_auth.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// URL of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiUrl =
        CVarDef.Create("discord_auth.api_url", "", CVar.SERVERONLY);

    /// <summary>
    /// Secret key of the Discord auth server API
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthApiKey =
        CVarDef.Create("discord_auth.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);


    /*
     * Hijack Volume
     */

    /// <summary>
    /// Default volume setting of Hijack Song
    /// </summary>
    public static readonly CVarDef<float> HijackVolume =
        CVarDef.Create("rmc.hijack_volume", 1.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Auto Climbing
     */

    /// <summary>
    /// Controls whether the player will automatically climb entities with the AutoClimbable component
    /// </summary>
    public static readonly CVarDef<bool> AutoClimb =
        CVarDef.Create("rmc.autoclimb", true, CVar.ARCHIVE | CVar.CLIENT | CVar.REPLICATED);

    /// <summary>
    /// Coefficient k in threshold = sqrt(online) * k.
    /// </summary>
    public static readonly CVarDef<float> HijackMarineSqrtCoeff =
        CVarDef.Create("st.hijack_marine_sqrt_coeff", 1.4f, CVar.SERVERONLY);

    /// <summary>
    /// Minimum threshold for the marine check, regardless of online count.
    /// </summary>
    public static readonly CVarDef<int> HijackMinMarinesFloor =
        CVarDef.Create("st.hijack_min_marines_floor", 3, CVar.SERVERONLY);

    /// <summary>
    /// Minutes after round start before the hijack refuse button becomes usable
    /// </summary>
    public static readonly CVarDef<float> HijackTimerMinutes =
        CVarDef.Create("st.hijack_timer_minutes", 45f, CVar.SERVERONLY);

    /*
     * Stories Hunter
     */

    /// <summary>
    /// Minimum players required to start a Hunter round
    /// </summary>
    public static readonly CVarDef<int> HunterMinPlayersForRound =
        CVarDef.Create("stories.hunter_min_players", 20, CVar.SERVERONLY);

    /// <summary>
    /// Chance for a Hunter round to start
    /// </summary>
    public static readonly CVarDef<float> HunterRoundChance =
        CVarDef.Create("stories.hunter_round_chance", 0.15f, CVar.SERVERONLY);

    /// <summary>
    /// Base number of hunter slots available at round start (Start Count).
    /// Formula: BaseSlots = StartCount + floor(Players / PlayerRatio).
    /// </summary>
    public static readonly CVarDef<int> HunterStartCount =
        CVarDef.Create("stories.hunter_start_count", 4, CVar.SERVERONLY);

    /// <summary>
    /// How many players are required for 1 extra hunter slot.
    /// DM: pred_per_players (80)
    /// </summary>
    public static readonly CVarDef<int> HunterPlayerRatio =
        CVarDef.Create("stories.hunter_player_ratio", 80, CVar.SERVERONLY);

    /// <summary>
    /// Additional slots reserved for sponsors (don't count towards the base formula limit).
    /// </summary>
    public static readonly CVarDef<int> HunterSponsorExtraSlots =
        CVarDef.Create("stories.hunter_sponsor_extra_slots", 2, CVar.SERVERONLY);

    /*
     * RMC Vehicles
     */

    /// <summary>
    /// Minimum player count required to spawn an APC.
    /// </summary>
    public static readonly CVarDef<int> RMCLowPopVehicle =
        CVarDef.Create("rmc.vehicle.low_pop", 100, CVar.SERVERONLY);

    /// <summary>
    /// Minimum player count required to spawn a Tank.
    /// </summary>
    public static readonly CVarDef<int> RMCHighPopVehicle =
        CVarDef.Create("rmc.vehicle.high_pop", 200, CVar.SERVERONLY);

    /// <summary>
    /// Toggle streamer mode for UI components
    /// </summary>
    public static readonly CVarDef<bool> StreamerModeEnabled =
        CVarDef.Create("stories.streamer_mode", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Path to a file containing words to be banned from chat
    /// </summary>
    public static readonly CVarDef<string> BanwordsFile =
        CVarDef.Create("stories.banwords_file", "", CVar.SERVERONLY);
}
