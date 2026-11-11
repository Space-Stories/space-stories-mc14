using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Chat;

[Serializable, NetSerializable]
public sealed class SyncBanwordsEvent : EntityEventArgs
{
    public List<string> Banwords;
    public SyncBanwordsEvent(List<string> banwords) { Banwords = banwords; }
}

[Serializable, NetSerializable]
public sealed class RequestBanwordsEvent : EntityEventArgs
{
}

