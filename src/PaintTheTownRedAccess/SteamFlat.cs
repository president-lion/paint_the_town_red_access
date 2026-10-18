using System.Runtime.InteropServices;
using System.Text;

namespace PaintTheTownRedAccess;

/// <summary>
/// Steam's flat C API, called straight from the steam_api64.dll the game already loaded and
/// initialised (SDK with ISteamUGC v015, ISteamUtils v010, ISteamFriends v017). Used for the
/// workshop queries because IL2CPP stripped the Steamworks.NET wrappers the game never calls
/// (SetSearchText, SetRankedByTrendDays, IsAPICallCompleted). Main thread only.
/// </summary>
internal static class SteamFlat
{
    private const string Dll = "steam_api64";
    public const uint AppId = 337320;

    // EUGCQuery
    public const int RankedByVote = 0, RankedByPublicationDate = 1, RankedByTrend = 3, RankedByTextSearch = 11;
    private const int MatchingItems = 0; // k_EUGCMatchingUGCType_Items

    [DllImport(Dll)] private static extern IntPtr SteamAPI_SteamUGC_v015();
    [DllImport(Dll)] private static extern IntPtr SteamAPI_SteamUtils_v010();
    [DllImport(Dll)] private static extern IntPtr SteamAPI_SteamFriends_v017();

    [DllImport(Dll)] private static extern ulong SteamAPI_ISteamUGC_CreateQueryAllUGCRequestPage(IntPtr self, int query, int matching, uint creatorApp, uint consumerApp, uint page);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUGC_AddRequiredTag(IntPtr self, ulong handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUGC_SetSearchText(IntPtr self, ulong handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUGC_SetRankedByTrendDays(IntPtr self, ulong handle, uint days);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUGC_SetReturnLongDescription(IntPtr self, ulong handle, [MarshalAs(UnmanagedType.I1)] bool longDescription);
    [DllImport(Dll)] private static extern ulong SteamAPI_ISteamUGC_SendQueryUGCRequest(IntPtr self, ulong handle);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUGC_GetQueryUGCResult(IntPtr self, ulong handle, uint index, IntPtr details);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUGC_ReleaseQueryUGCRequest(IntPtr self, ulong handle);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamUtils_IsAPICallCompleted(IntPtr self, ulong call, [MarshalAs(UnmanagedType.I1)] out bool failed);
    [DllImport(Dll)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SteamAPI_ISteamFriends_RequestUserInformation(IntPtr self, ulong steamId, [MarshalAs(UnmanagedType.I1)] bool nameOnly);
    [DllImport(Dll)] private static extern IntPtr SteamAPI_ISteamFriends_GetFriendPersonaName(IntPtr self, ulong steamId);

    /// <summary>One workshop item from a query.</summary>
    public sealed class Item
    {
        public ulong Id;
        public string Title = "";
        public string Description = "";
        public ulong Owner;
        public uint Created, VotesUp, VotesDown;
        public int FileSize;
    }

    /// <summary>A running query: poll <see cref="Poll"/> until it is no longer null.</summary>
    public sealed class Query
    {
        public ulong Handle, Call;
        public bool Done, Failed;
        public List<Item> Items = new();
        public float StartedAt;
    }

    /// <summary>Starts a query for workshop levels: 50 a page, page from 1.</summary>
    public static Query Start(int rank, uint page, string search)
    {
        var ugc = SteamAPI_SteamUGC_v015();
        if (ugc == IntPtr.Zero) return null;
        var h = SteamAPI_ISteamUGC_CreateQueryAllUGCRequestPage(ugc, rank, MatchingItems, AppId, AppId, page);
        if (h == ulong.MaxValue) return null; // k_UGCQueryHandleInvalid
        SteamAPI_ISteamUGC_AddRequiredTag(ugc, h, "Level");
        if (rank == RankedByTrend) SteamAPI_ISteamUGC_SetRankedByTrendDays(ugc, h, 30);
        if (!string.IsNullOrWhiteSpace(search)) SteamAPI_ISteamUGC_SetSearchText(ugc, h, search);
        SteamAPI_ISteamUGC_SetReturnLongDescription(ugc, h, false);
        var call = SteamAPI_ISteamUGC_SendQueryUGCRequest(ugc, h);
        Log.Info("Workshop", $"query rank={rank} page={page} search='{search}' handle={h} call={call}");
        if (call == 0) { SteamAPI_ISteamUGC_ReleaseQueryUGCRequest(ugc, h); return null; }
        return new Query { Handle = h, Call = call, StartedAt = UnityEngine.Time.unscaledTime };
    }

    /// <summary>Checks a query; once Steam has answered, reads its results and frees it. True when done.</summary>
    public static bool Poll(Query q)
    {
        if (q.Done) return true;
        var utils = SteamAPI_SteamUtils_v010();
        if (!SteamAPI_ISteamUtils_IsAPICallCompleted(utils, q.Call, out var failed)) return false;
        q.Done = true;
        var ugc = SteamAPI_SteamUGC_v015();
        // The game's own Steamworks.NET dispatcher takes every finished call off the queue and
        // drops results nobody registered for, so this call often reads as failed. The results
        // stay on the query handle until it is released, so read them anyway.
        ReadResults(ugc, q);
        q.Failed = q.Items.Count == 0 && failed;
        SteamAPI_ISteamUGC_ReleaseQueryUGCRequest(ugc, q.Handle);
        Log.Info("Workshop", $"query {q.Handle} done callFailed={failed} items={q.Items.Count}");
        return true;
    }

    // SteamUGCDetails_t as laid out by this SDK (Windows packs Steam structs to 8 bytes).
    private const int DetailsSize = 9776;
    private const int OffResult = 8, OffConsumer = 20, OffTitle = 24, TitleLen = 129, OffDesc = 153, DescLen = 8000,
        OffOwner = 8160, OffCreated = 8168, OffFileSize = 9492, OffVotesUp = 9756, OffVotesDown = 9760;

    private static void ReadResults(IntPtr ugc, Query q)
    {
        var buf = Marshal.AllocHGlobal(DetailsSize + 1024);
        try
        {
            for (uint i = 0; i < 50; i++)
            {
                if (!SteamAPI_ISteamUGC_GetQueryUGCResult(ugc, q.Handle, i, buf)) break;
                if (Marshal.ReadInt32(buf, OffResult) != 1) continue; // k_EResultOK
                var it = new Item
                {
                    Id = (ulong)Marshal.ReadInt64(buf, 0),
                    Title = Utf8(buf, OffTitle, TitleLen),
                    Description = Utf8(buf, OffDesc, DescLen),
                    Owner = (ulong)Marshal.ReadInt64(buf, OffOwner),
                    Created = (uint)Marshal.ReadInt32(buf, OffCreated),
                    FileSize = Marshal.ReadInt32(buf, OffFileSize),
                    VotesUp = (uint)Marshal.ReadInt32(buf, OffVotesUp),
                    VotesDown = (uint)Marshal.ReadInt32(buf, OffVotesDown),
                };
                if (i == 0)
                    Log.Info("Workshop", $"first result: id={it.Id} app={(uint)Marshal.ReadInt32(buf, OffConsumer)} title='{it.Title}' owner={it.Owner} up={it.VotesUp} down={it.VotesDown} size={it.FileSize}");
                q.Items.Add(it);
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static string Utf8(IntPtr buf, int offset, int max)
    {
        var bytes = new byte[max];
        Marshal.Copy(buf + offset, bytes, 0, max);
        var len = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, len < 0 ? max : len);
    }

    /// <summary>A Steam user's name, or null while Steam is still fetching it.</summary>
    public static string PersonaName(ulong steamId)
    {
        if (steamId == 0) return null;
        var friends = SteamAPI_SteamFriends_v017();
        if (friends == IntPtr.Zero) return null;
        // True means "requested, not known yet"; false means the name is already cached.
        if (SteamAPI_ISteamFriends_RequestUserInformation(friends, steamId, true)) return null;
        var name = Marshal.PtrToStringUTF8(SteamAPI_ISteamFriends_GetFriendPersonaName(friends, steamId));
        return string.IsNullOrWhiteSpace(name) || name == "[unknown]" ? null : name;
    }
}
