using Il2Cpp;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PaintTheTownRedAccess;

/// <summary>
/// The Player Created Levels screen as a spoken list. The game's own screen is a grid of
/// thumbnails that keyboard navigation never reaches, so this opens by itself when that
/// screen shows (L reopens it). Tabs, with Left/Right:
///   subscribed, favourites   the panel's own lists (FinalMenuUGCLevels), else SEGSteamUGC.InstalledLevels
///   popular, top rated, newest, search   live Steam Workshop queries (SteamFlat), 50 a page,
///                                        "more results" at the end loads the next page
/// Up/Down/Home/End move. Enter plays a subscribed level (GameManager.PlayUGCLevel, the same
/// call the thumbnail's play button makes) or subscribes to one that is not (SEGSteamUGC.
/// SubscribeToItem; Steam downloads it and it joins the subscribed tab). F favourite,
/// U / D vote up / down, Delete twice unsubscribes, I details. On the search tab the first
/// row is a search box: type, then Enter. Escape closes (and a second Escape goes back as
/// usual). The game's menu navigation and input are held while it is open.
/// </summary>
internal static class WorkshopMenu
{
    private const int TabSubscribed = 0, TabFavourites = 1, TabPopular = 2, TabTop = 3, TabNewest = 4, TabSearch = 5;
    private static readonly string[] TabNames =
        { Strings.WsSubscribed, Strings.WsFavourites, Strings.WsPopular, Strings.WsTopRated, Strings.WsNewest, Strings.WsSearch };

    private sealed class Entry
    {
        public ulong Id;
        public string Title = "";
        public string Description = "";
        public ulong Owner;
        public uint VotesUp, VotesDown, Created;
        public int FileSize;
        public bool Played = true;
    }

    private sealed class Tab
    {
        public readonly List<Entry> Items = new();
        public bool Loaded;
        public uint Page;
        public bool HasMore;
        public SteamFlat.Query Pending;
    }

    private static readonly Tab[] _tabs = { new(), new(), new(), new(), new(), new() };
    private static int _tab, _index;
    private static string _search = "";
    private static string _searchRan;
    private static ulong _confirmUnsub;
    private static float _confirmUntil;
    private static bool _hinted;

    private static FinalMenuUGCLevels _panel;
    private static bool _panelShowing;
    private static float _nextPanelCheck;
    private static bool _navWas = true;
    private static float _closedAt = -10f;

    public static bool Open { get; private set; }

    /// <summary>True while open and until the key that closed it is released.</summary>
    public static bool BlockingInput =>
        Open || (Time.unscaledTime - _closedAt < 1f &&
                 (Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space)));

    public static void OnSceneLoaded()
    {
        if (Open) Close(silent: true);
        _panel = null;
        _panelShowing = false;
    }

    public static void Tick()
    {
        if (Game.InPlay) { if (Open) Close(silent: true); return; }
        WatchPanel();
        if (!Open)
        {
            if (_panelShowing && Input.GetKeyDown(KeyCode.L)) Show();
            return;
        }

        PollQueries();
        var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(silent: false); return; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) SwitchTab(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) SwitchTab(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
        else if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
        else if (Input.GetKeyDown(KeyCode.Home)) Move(-10000);
        else if (Input.GetKeyDown(KeyCode.End)) Move(10000);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Activate();
        else if (OnSearchBox) TypeSearch();
        else if (ctrl) { }
        else if (Input.GetKeyDown(KeyCode.Space)) Activate();
        else if (Input.GetKeyDown(KeyCode.F)) Favourite();
        else if (Input.GetKeyDown(KeyCode.U)) Vote(true);
        else if (Input.GetKeyDown(KeyCode.D)) Vote(false);
        else if (Input.GetKeyDown(KeyCode.Delete)) Unsubscribe();
        else if (Input.GetKeyDown(KeyCode.I)) Details();
    }

    // ---- the game's screen -------------------------------------------------------------------

    private static void WatchPanel()
    {
        var now = Time.unscaledTime;
        if (now < _nextPanelCheck) return;
        _nextPanelCheck = now + 0.25f;
        if (_panel == null)
            foreach (var p in UnityEngine.Object.FindObjectsOfType<FinalMenuUGCLevels>())
                // The co-op copy of the screen only picks a level for the host; leave it to the game.
                if (p != null && !UiDump.PathOf(p.transform).Contains("ForCoop")) { _panel = p; break; }
        var showing = _panel != null && _panel.gameObject.activeInHierarchy && UiText.Visible(_panel.transform);
        if (showing && !_panelShowing) { _panelShowing = true; Show(); }
        else if (!showing && _panelShowing)
        {
            _panelShowing = false;
            if (Open) Close(silent: true);
        }
    }

    private static void Show()
    {
        Open = true;
        var es = EventSystem.current;
        if (es != null) { _navWas = es.sendNavigationEvents; es.sendNavigationEvents = false; }
        LoadLocal(TabSubscribed);
        LoadLocal(TabFavourites);
        _tab = _tabs[TabSubscribed].Items.Count > 0 ? TabSubscribed : TabPopular;
        _index = 0;
        var line = Strings.WsOpen + " " + TabLine();
        if (!_hinted) { line += " " + Strings.WsHint; _hinted = true; }
        Speech.Say(line);
        EnterTab();
    }

    private static void Close(bool silent)
    {
        Open = false;
        _closedAt = Time.unscaledTime;
        var es = EventSystem.current;
        if (es != null) es.sendNavigationEvents = _navWas;
        if (!silent) Speech.Say(Strings.WsClosed);
    }

    // ---- lists ---------------------------------------------------------------------------------

    private static bool Online(int tab) => tab >= TabPopular;
    private static Tab Cur => _tabs[_tab];
    private static bool OnSearchBox => _tab == TabSearch && _index == 0;
    // The search tab has the search box as row 0; online tabs with more pages end in "more results".
    private static int RowCount => (_tab == TabSearch ? 1 : 0) + Cur.Items.Count + (Cur.HasMore ? 1 : 0);
    private static int FirstItemRow => _tab == TabSearch ? 1 : 0;

    private static Entry EntryAt(int row)
    {
        var i = row - FirstItemRow;
        return i >= 0 && i < Cur.Items.Count ? Cur.Items[i] : null;
    }

    private static bool OnMoreRow => Cur.HasMore && _index == RowCount - 1;

    private static void LoadLocal(int tab)
    {
        var t = _tabs[tab];
        t.Items.Clear();
        t.Loaded = true;
        Il2CppSystem.Collections.Generic.List<SEGSteamUGC.UGCInstalled> list = null;
        if (_panel != null) list = tab == TabSubscribed ? _panel.subscribedLevelsList : _panel.favouritesLevelsList;
        if (list == null || list.Count == 0)
        {
            list = SEGSteamUGC.InstalledLevels;
            if (list == null) return;
            foreach (var l in list)
                if (l != null && l.IsLevel() && (tab == TabSubscribed ? l.isSubscribed : l.isFavourited)) t.Items.Add(FromInstalled(l));
            return;
        }
        foreach (var l in list) if (l != null) t.Items.Add(FromInstalled(l));
    }

    private static Entry FromInstalled(SEGSteamUGC.UGCInstalled l) => new()
    {
        Id = l.publishedFileId.m_PublishedFileId,
        Title = l.title ?? "",
        Owner = l.ownerId,
        VotesUp = l.votesUp,
        VotesDown = l.votesDown,
        Created = l.timeCreated,
        FileSize = l.fileSize,
        Played = l.hasPlayed,
    };

    private static SEGSteamUGC.UGCInstalled Installed(ulong id)
    {
        var list = SEGSteamUGC.InstalledLevels;
        if (list == null) return null;
        foreach (var l in list) if (l != null && l.publishedFileId.m_PublishedFileId == id) return l;
        return null;
    }

    private static bool IsSubscribed(ulong id)
    {
        var l = Installed(id);
        if (l != null) return l.isSubscribed;
        var subs = SEGSteamUGC.SubscribedLevels;
        if (subs == null) return false;
        foreach (var s in subs) if (s.m_PublishedFileId == id) return true;
        return false;
    }

    private static bool IsFavourite(ulong id)
    {
        var l = Installed(id);
        if (l != null && l.isFavourited) return true;
        var favs = SEGSteamUGC.FavouritedLevels;
        if (favs == null) return false;
        foreach (var f in favs) if (f.m_PublishedFileId == id) return true;
        return false;
    }

    private static int RankOf(int tab) => tab switch
    {
        TabPopular => SteamFlat.RankedByTrend,
        TabTop => SteamFlat.RankedByVote,
        TabNewest => SteamFlat.RankedByPublicationDate,
        _ => SteamFlat.RankedByTextSearch,
    };

    private static void Query(int tab, uint page)
    {
        var t = _tabs[tab];
        if (t.Pending != null) return;
        t.Pending = SteamFlat.Start(RankOf(tab), page, tab == TabSearch ? _search : null);
        if (t.Pending == null) { Speech.Say(Strings.WsOffline); return; }
        t.Page = page;
        Speech.Say(Strings.WsLoading, interrupt: false);
    }

    private static void PollQueries()
    {
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            var t = _tabs[tab];
            var q = t.Pending;
            if (q == null) continue;
            if (!SteamFlat.Poll(q))
            {
                if (Time.unscaledTime - q.StartedAt > 20f) { t.Pending = null; if (tab == _tab) Speech.Say(Strings.WsOffline); }
                continue;
            }
            t.Pending = null;
            t.Loaded = true;
            if (q.Failed) { if (tab == _tab) Speech.Say(Strings.WsOffline); continue; }
            var firstNew = t.Items.Count;
            foreach (var it in q.Items)
            {
                t.Items.Add(new Entry
                {
                    Id = it.Id, Title = it.Title, Description = it.Description, Owner = it.Owner,
                    VotesUp = it.VotesUp, VotesDown = it.VotesDown, Created = it.Created, FileSize = it.FileSize,
                });
                SteamFlat.PersonaName(it.Owner); // ask Steam for the name now so it is ready when read
            }
            t.HasMore = q.Items.Count >= 50;
            if (tab != _tab) continue;
            if (t.Page > 1)
            {
                _index = FirstItemRow + firstNew;
                Speech.Say(string.Format(Strings.WsMoreLoaded, q.Items.Count) + " " + Line());
            }
            else
            {
                _index = FirstItemRow;
                Speech.Say(TabLine() + (t.Items.Count > 0 ? " " + Line() : ""));
            }
        }
    }

    // ---- moving --------------------------------------------------------------------------------

    private static void SwitchTab(int step)
    {
        _tab = (_tab + step + _tabs.Length) % _tabs.Length;
        _index = 0;
        _confirmUnsub = 0;
        Speech.Say(TabLine());
        EnterTab();
    }

    /// <summary>Speaks the first row of the tab, or starts loading it.</summary>
    private static void EnterTab()
    {
        if (!Online(_tab)) LoadLocal(_tab);
        if (_tab == TabSearch) { Speech.Say(Line(), interrupt: false); return; }
        if (Online(_tab) && !Cur.Loaded) { Query(_tab, 1); return; }
        if (Cur.Pending != null) { Speech.Say(Strings.WsLoading, interrupt: false); return; }
        Speech.Say(RowCount == 0 ? Strings.WsEmpty : Line(), interrupt: false);
    }

    private static string TabLine()
    {
        var t = Cur;
        var count = t.Items.Count;
        var what = !Online(_tab) || t.Loaded
            ? (count == 1 ? Strings.WsOneLevel : string.Format(Strings.WsLevels, count))
            : "";
        var head = $"{TabNames[_tab]}, {string.Format(Strings.Position, _tab + 1, _tabs.Length)}";
        return string.IsNullOrEmpty(what) ? head + "." : $"{head}, {what}.";
    }

    private static void Move(int step)
    {
        if (RowCount == 0) { Speech.Say(Cur.Pending != null ? Strings.WsLoading : Strings.WsEmpty); return; }
        _index = Math.Clamp(_index + step, 0, RowCount - 1);
        _confirmUnsub = 0;
        Speech.Say(Line());
    }

    private static string Line()
    {
        if (OnSearchBox)
            return string.Format(Strings.WsSearchBox, string.IsNullOrEmpty(_search) ? Strings.WsBlank : _search);
        var pos = string.Format(Strings.Position, _index + 1, RowCount);
        if (OnMoreRow) return $"{Strings.WsMore}, {pos}";
        var e = EntryAt(_index);
        if (e == null) return Strings.WsEmpty;
        var parts = new List<string> { string.IsNullOrWhiteSpace(e.Title) ? Strings.WsUntitled : e.Title.Trim() };
        var by = SteamFlat.PersonaName(e.Owner);
        if (by != null) parts[0] = string.Format(Strings.By, parts[0], by);
        if (Online(_tab))
        {
            if (IsSubscribed(e.Id)) parts.Add(Strings.WsIsSubscribed);
        }
        else if (!e.Played) parts.Add(Strings.WsNotPlayed);
        if (_tab != TabFavourites && IsFavourite(e.Id)) parts.Add(Strings.WsIsFavourite);
        parts.Add(pos);
        return string.Join(", ", parts);
    }

    // ---- acting --------------------------------------------------------------------------------

    private static void TypeSearch()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_search.Length == 0) return;
            var gone = _search[^1];
            _search = _search[..^1];
            Speech.Say(gone == ' ' ? Strings.WsSpace : gone.ToString());
            return;
        }
        foreach (var c in Input.inputString)
        {
            if (char.IsControl(c)) continue;
            if (_search.Length >= 100) break;
            _search += c;
            Speech.Say(c == ' ' ? Strings.WsSpace : c.ToString());
        }
    }

    private static void Activate()
    {
        if (OnSearchBox)
        {
            if (string.IsNullOrWhiteSpace(_search)) { Speech.Say(Strings.WsTypeFirst); return; }
            if (_search == _searchRan && Cur.Pending == null && Cur.Items.Count > 0) { Move(1); return; }
            _searchRan = _search;
            Cur.Items.Clear();
            Cur.HasMore = false;
            Query(TabSearch, 1);
            return;
        }
        if (OnMoreRow) { Query(_tab, Cur.Page + 1); return; }
        var e = EntryAt(_index);
        if (e == null) return;

        var installed = Installed(e.Id);
        if (installed != null && installed.IsAvailable())
        {
            Log.Info("Workshop", $"play {e.Id} '{e.Title}'");
            Speech.Say(string.Format(Strings.Loading, e.Title));
            Close(silent: true);
            GameManager.PlayUGCLevel(installed);
            return;
        }
        if (IsSubscribed(e.Id) || SEGSteamUGC.IsItemDownloading(e.Id))
        {
            var pct = Mathf.RoundToInt(SEGSteamUGC.GetDownloadProgress(e.Id) * 100f);
            Speech.Say(string.Format(Strings.WsDownloading, e.Title, pct));
            return;
        }
        Log.Info("Workshop", $"subscribe {e.Id} '{e.Title}'");
        SEGSteamUGC.SubscribeToItem(e.Id);
        Cues.Play2D(Cues.Cue.Arrive, 0.6f);
        Speech.Say(string.Format(Strings.WsSubscribing, e.Title));
    }

    private static void Favourite()
    {
        var e = EntryAt(_index);
        if (e == null) return;
        var add = !IsFavourite(e.Id);
        SEGSteamUGC.FavouriteItem(e.Id, add);
        Log.Info("Workshop", $"favourite {e.Id} add={add}");
        Speech.Say(string.Format(add ? Strings.WsFavAdded : Strings.WsFavRemoved, e.Title));
    }

    private static void Vote(bool up)
    {
        var e = EntryAt(_index);
        if (e == null) return;
        SEGSteamUGC.VoteOnItem(e.Id, up);
        Log.Info("Workshop", $"vote {e.Id} up={up}");
        Speech.Say(string.Format(up ? Strings.WsVotedUp : Strings.WsVotedDown, e.Title));
    }

    private static void Unsubscribe()
    {
        var e = EntryAt(_index);
        if (e == null) return;
        if (!IsSubscribed(e.Id)) { Speech.Say(Strings.WsNotSubscribed); return; }
        var now = Time.unscaledTime;
        if (_confirmUnsub != e.Id || now > _confirmUntil)
        {
            _confirmUnsub = e.Id;
            _confirmUntil = now + 4f;
            Speech.Say(string.Format(Strings.WsConfirmUnsub, e.Title));
            return;
        }
        _confirmUnsub = 0;
        SEGSteamUGC.UnsubscribeFromItem(e.Id);
        Log.Info("Workshop", $"unsubscribe {e.Id}");
        Speech.Say(string.Format(Strings.WsUnsubscribed, e.Title));
        if (_tab == TabSubscribed)
        {
            Cur.Items.Remove(e);
            _index = Math.Clamp(_index, 0, Math.Max(0, RowCount - 1));
        }
    }

    private static void Details()
    {
        var e = EntryAt(_index);
        if (e == null) { Speech.Say(Line()); return; }
        var parts = new List<string> { string.Format(Strings.WsVotes, e.VotesUp, e.VotesDown) };
        if (e.Created > 0)
            parts.Add(string.Format(Strings.WsMade, DateTimeOffset.FromUnixTimeSeconds(e.Created).ToString("MMMM yyyy")));
        if (e.FileSize > 0) parts.Add(string.Format(Strings.WsSize, Math.Max(1, e.FileSize / (1024 * 1024))));
        var line = string.Join(", ", parts) + ".";
        if (!string.IsNullOrWhiteSpace(e.Description)) line += " " + TextClean.Clean(e.Description);
        Speech.Say(line);
    }
}
