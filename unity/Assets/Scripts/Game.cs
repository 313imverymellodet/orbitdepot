using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TutStep { Move, TakePallet, StorePallet, GrabCartons, Restock, Pick, DropTotes, Pack, TakeParcels, Sort, TakeBin, Ship, Collect, Buy, Done }

[Serializable]
public class SaveData
{
    public int depot;
    public double money, lifetime, rate;
    public List<string> done = new List<string>();
    public List<string> padIds = new List<string>();
    public List<double> padPaid = new List<double>();
    public int lvSpeed, lvCap, lvProfit, lvStaff;
    public long last;
    public int tut, quest;
    public bool muted;
    public int shipped, packed, pShipped, opened;
    public double pEarned;
    // live floor state
    public int loose, rackStock, orders, chute, cart, infeed, outfeed;
    public List<int> ready = new List<int>(), bulk = new List<int>(), bins = new List<int>();
    public List<int> hands = new List<int>();     // carried items (type); bulky ones store their box count in handLoad
    public int handLoad;
    public double cashPile;
    public float powerLeft, powerCd = -1f, rushLeft;
    public float px, pz; public bool hasPos;
}

// ORBIT DEPOT: run a fulfilment floor on a space station. Receive, store, restock, pick, pack, sort, ship.
public class Game : MonoBehaviour
{
    public static Game I;

    // ------------------------------------------------ depots (prestige)
    struct Depot { public string name; public Color floorA, floorB, line, accent, sky, wall; }
    static readonly Depot[] Depots =
    {
        new Depot { name = "LUNA DEPOT", floorA = Kit.Hex("#43536f"), floorB = Kit.Hex("#3e4d68"), line = Kit.Hex("#56688a"), accent = Kit.Hex("#ffc93c"), sky = Kit.Hex("#070b18"), wall = Kit.Hex("#1a2540") },
        new Depot { name = "MARS HUB", floorA = Kit.Hex("#5a4b52"), floorB = Kit.Hex("#54464c"), line = Kit.Hex("#73606a"), accent = Kit.Hex("#ff8a3c"), sky = Kit.Hex("#160810"), wall = Kit.Hex("#2e1d26") },
        new Depot { name = "EUROPA PORT", floorA = Kit.Hex("#3f5a66"), floorB = Kit.Hex("#3a5460"), line = Kit.Hex("#557784"), accent = Kit.Hex("#5ce1ff"), sky = Kit.Hex("#04121a"), wall = Kit.Hex("#163038") },
    };
    Depot D => Depots[Save.depot % Depots.Length];
    public string DepotName => D.name + (Save.depot >= Depots.Length ? " " + (Save.depot / Depots.Length + 1) : "");

    // ------------------------------------------------ state
    public SaveData Save;
    public Camera Cam;
    public Player Player;
    public InboundDock Dock;
    public BulkStorage Bulk;
    public PickRack Rack;
    public PackLine Pack;
    public Sorter Sort;
    public OutboundDock Out;
    public PowerPack Power;
    public readonly List<Worker> Workers = new List<Worker>();
    readonly List<Pad> pads = new List<Pad>();
    Transform world;
    Light sun;

    public double Money => Save.money;
    public double DepotMult => Math.Pow(4, Save.depot);
    public double BillValue => 5 * DepotMult;
    public bool AutoBank => Save.done.Contains("autobank");
    bool Express => Save.done.Contains("express");
    public double BoxPrice => 8 * DepotMult * (1 + 0.2 * Save.lvProfit) * (PowerLeft > 0 ? 2 : 1) * (Express ? 1.25 : 1) * (RushLeft > 0 ? 1.5 : 1);
    public float MoveSpeed => 4.6f * (1f + 0.1f * Save.lvSpeed);
    public int Capacity => 4 + 2 * Save.lvCap;
    public float StaffSpeed => 1f + 0.2f * Save.lvStaff;
    public float CrewSpeed => Save.done.Contains("crew2") ? 1.7f : 1f;
    public int TruckBonus => Save.done.Contains("crew2") ? 4 : 0;
    public float TruckInterval => Save.done.Contains("crew2") ? 7f : 12f;
    public float PickTime => 0.42f;
    public float PackSpeed => 1.15f * (PowerLeft > 0 ? 2f : 1f);
    public float SortTime => 0.32f;
    public int Orders;
    public float RushLeft;
    float rushT = 160f, orderT = 2f;

    public bool AutoPlay, Dev;
    public Zone TerminalZone;
    public string Hint;
    public Vector3? ArrowTarget;
    public float PowerLeft, PowerCooldown;
    public bool PowerReady => Power && PowerLeft <= 0 && PowerCooldown <= 0;

    float saveT, rateT, autoBuyT;
    double earnedThisSecond;
    bool dirty, launching, restoring, hadSave;
    static readonly int[] UpgradeMax = { 6, 8, 10, 6 };

    struct PadDef { public string id; public double cost; public Vector3 pos; public string label; public Action build; }
    List<PadDef> padDefs;

    // ======================================================================
    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 45f; QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.antiAliasing = 2;

        Load();
        var url = Application.absoluteURL;
        Dev = url.Contains("dev=1") && (url.Contains("://localhost") || url.Contains("://127.0.0.1"));
        DevCam.Install(Dev);
        AutoPlay = url.Contains("bot=1");
        if (Dev && url.Contains("fresh=1")) Save = new SaveData();
        var sm = System.Text.RegularExpressions.Regex.Match(url, @"speed=(\d+)");
        if (Dev && sm.Success) Time.timeScale = Mathf.Clamp(int.Parse(sm.Groups[1].Value), 1, 8);
        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();

        Cam = Camera.main;
        Cam.fieldOfView = 38f; Cam.nearClipPlane = 0.5f; Cam.farClipPlane = 140f;
        Cam.clearFlags = CameraClearFlags.SolidColor;

        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(58, -28, 0);
        sun.shadows = LightShadows.Hard; sun.shadowStrength = 0.5f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
        sun.intensity = 0.9f; sun.color = Kit.Hex("#eef3ff");

        new GameObject("UI").AddComponent<UI>().Init();
        BuildWorld();
        RestoreState();
        if (hadSave && !OfflineEarnings()) Toast("WELCOME BACK, BOSS!");
        WebBridge.Ready();
        WebBridge.Gameplay(true);
    }

    // ======================================================================
    // Save / load
    void Load()
    {
        SaveData a = Parse(PlayerPrefs.GetString("od_save", "")), b = Parse(WebBridge.LoadMirror("od_save"));
        Save = a == null ? b : b == null ? a : (b.last > a.last ? b : a);
        if (Save == null) Save = new SaveData();
        hadSave = Save.lifetime > 0 || Save.done.Count > 0 || Save.depot > 0;
    }

    static SaveData Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<SaveData>(json); } catch { return null; }
    }

    public void Persist()
    {
        if (Save == null || launching) return;
        Snapshot();
        Save.last = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = JsonUtility.ToJson(Save);
        PlayerPrefs.SetString("od_save", json);
        PlayerPrefs.Save();
        WebBridge.SaveMirror("od_save", json);
        dirty = false;
    }

    static List<int> Loads(ItemStack s) { var l = new List<int>(); for (int i = 0; i < s.Count; i++) l.Add(s.At(i).GetComponent<Payload>().Count); return l; }

    void Snapshot()
    {
        if (!world) return;
        Save.loose = Dock.Loose.Count;
        Save.ready = Loads(Dock.Ready);
        Save.bulk = Loads(Bulk.Slots);
        Save.rackStock = Rack.Stock; Save.orders = Orders;
        Save.chute = Sort.Chute.Count; Save.cart = Pack.Cart.Count;
        var po = Pack.SaveString().Split(','); Save.infeed = int.Parse(po[0]); Save.outfeed = int.Parse(po[1]);
        Save.bins.Clear(); foreach (var b in Sort.Bins) Save.bins.Add(b ? b.GetComponent<Payload>().Count : 0);
        Save.hands.Clear(); Save.handLoad = 0;
        foreach (var t in Player.Stack.Types()) Save.hands.Add((int)t);
        if (!Player.Stack.Empty && Items.Bulky(Player.Stack.TopType)) Save.handLoad = Player.Stack.Top.GetComponent<Payload>().Count;
        Save.cashPile = Out.Cash.Value;
        Save.powerLeft = PowerLeft; Save.powerCd = PowerCooldown; Save.rushLeft = RushLeft;
        Save.px = Player.transform.position.x; Save.pz = Player.transform.position.z; Save.hasPos = true;
    }

    void RestoreState()
    {
        if (!hadSave)
        {
            // a fresh floor: a pallet waiting at the dock and orders already coming in, so the first seconds have something to do
            AddPallets(Dock.Ready, new List<int> { 8 });
            for (int i = 0; i < 6; i++) Dock.Loose.AddInstant(Item.Carton);
            Orders = 6;
            return;
        }
        for (int i = 0; i < Save.loose && i < Dock.LooseCap; i++) Dock.Loose.AddInstant(Item.Carton);
        AddPallets(Dock.Ready, Save.ready);
        AddPallets(Bulk.Slots, Save.bulk);
        Rack.Stock = Mathf.Clamp(Save.rackStock, 0, Rack.Cap);
        Orders = Mathf.Clamp(Save.orders, 0, 40);
        for (int i = 0; i < Save.chute; i++) Sort.Chute.AddInstant(Item.Parcel);
        for (int i = 0; i < Save.cart && i < Pack.Cart.Capacity; i++) Pack.Cart.AddInstant(Item.Parcel);
        Pack.Restore(Save.infeed, Save.outfeed);
        for (int i = 0; i < 4 && i < Save.bins.Count; i++) { if (Sort.Bins[i]) ItemPool.Release(Item.Gaylord, Sort.Bins[i]); Sort.NewBin(i, Save.bins[i]); }
        foreach (var h in Save.hands)
        {
            if (!Enum.IsDefined(typeof(Item), h)) continue;
            var it = (Item)h;
            if (!Player.Stack.CanAccept(it)) continue;
            Player.Stack.AddInstant(it);
            if (Items.Bulky(it)) Player.Stack.Top.GetComponent<Payload>().Set(Save.handLoad);
        }
        if (Save.cashPile > 0) Out.Cash.Deposit(Save.cashPile, Out.Cash.transform.position + Vector3.up);
        if (Save.powerCd >= 0) { PowerLeft = Save.powerLeft; PowerCooldown = Save.powerCd; }
        RushLeft = Save.rushLeft;
        if (Save.hasPos) Player.Teleport(new Vector3(Mathf.Clamp(Save.px, -14f, 14f), 0, Mathf.Clamp(Save.pz, -10f, 10f)));
    }

    static void AddPallets(ItemStack s, List<int> loads)
    {
        foreach (var n in loads)
        {
            if (s.Full || n <= 0) continue;
            s.AddInstant(Item.Pallet);
            s.Top.GetComponent<Payload>().Set(n);
        }
    }

    void OnApplicationPause(bool p) { if (p) Persist(); }
    void OnApplicationFocus(bool f) { if (!f) Persist(); }
    void OnApplicationQuit() => Persist();

    public void SavePad(string id, double paid)
    {
        int i = Save.padIds.IndexOf(id);
        if (i < 0) { Save.padIds.Add(id); Save.padPaid.Add(paid); } else Save.padPaid[i] = paid;
        dirty = true;
    }
    double PadPaid(string id) { int i = Save.padIds.IndexOf(id); return i < 0 ? 0 : Save.padPaid[i]; }

    bool OfflineEarnings()
    {
        if (Save.last <= 0 || Save.rate <= 0) return false;
        double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Save.last;
        if (away < 90) return false;
        double amount = Math.Floor(Save.rate * Math.Min(away, 3 * 3600) * 0.35);
        if (amount < 5) return false;
        string span = away >= 3600 ? (away / 3600).ToString("0.#") + "h" : Math.Round(away / 60) + " min";
        string body = "Your crew kept shipping for " + span + ".\nYou earned " + Kit.Money(amount) + "!";
        if (WebBridge.AdsAvailable)
            UI.I.Confirm("WELCOME BACK!", body, "COLLECT", () => AddMoney(amount, true),
                "COLLECT 2× (" + Kit.Money(amount * 2) + ")", () => WebBridge.I.ShowRewarded(ok => { AddMoney(ok ? amount * 2 : amount, true); if (ok) WebBridge.Event("ad_offline2x"); }));
        else UI.I.Confirm("WELCOME BACK!", body, "COLLECT", () => AddMoney(amount, true));
        return true;
    }

    // ======================================================================
    // Economy
    public void AddMoney(double v, bool earned)
    {
        Save.money = Math.Max(0, Save.money + v);
        if (earned && v > 0) { Save.lifetime += v; Save.pEarned += v; earnedThisSecond += v; UI.I.PunchMoney(); }
        dirty = true;
    }

    public void Shipped(int boxes, double pay)
    {
        Save.pShipped += boxes;
        WebBridge.Event("ship_bin", boxes);
        dirty = true;
    }

    public double UpgradeCost(int kind)
    {
        double[] b = { 40, 50, 90, 150 };
        int lv = kind switch { 0 => Save.lvSpeed, 1 => Save.lvCap, 2 => Save.lvProfit, _ => Save.lvStaff };
        return Math.Round(b[kind] * DepotMult * Math.Pow(1.85, lv));
    }

    public double CheapestUpgrade(out int kind)
    {
        kind = -1; double best = double.MaxValue;
        int[] lv = { Save.lvSpeed, Save.lvCap, Save.lvProfit, Save.lvStaff };
        for (int k = 0; k < (Workers.Count > 1 ? 4 : 3); k++)
            if (lv[k] < UpgradeMax[k] && UpgradeCost(k) < best) { best = UpgradeCost(k); kind = k; }
        return best;
    }
    public int StaffCount => Workers.Count - 1;   // ELI the dock crew is always there

    public void RequestPower()
    {
        if (!PowerReady) return;
        if (!WebBridge.AdsAvailable) { GrantPower(60f, 240f); return; }
        WebBridge.I.ShowRewarded(ok => { if (ok) { GrantPower(75f, 180f); WebBridge.Event("ad_power"); } });
    }

    void GrantPower(float secs, float cooldown)
    {
        PowerLeft = secs; PowerCooldown = cooldown;
        Sfx.I.LevelUp();
        Burst(Power.transform.position, Kit.Hex("#ff5c9a"));
        UI.I.FloatText(Player.transform.position + Vector3.up * 2.6f, "POWER PACK!  2× PACKING · 2× PAY", Kit.Hex("#ff8ac0"), 54);
    }

    // ======================================================================
    // World
    static Material steel, rackBlue, rackOrange, shelf, wallMat, trimMat, glowMat;
    static Material M(ref Material m, Color c, float g = 0.3f, bool emit = false)
    {
        if (!m) { m = new Material(Shader.Find("Standard")) { color = c }; m.SetFloat("_Glossiness", g); if (emit) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.8f); } }
        return m;
    }

    GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!collider) Destroy(go.GetComponent<Collider>());
        return go;
    }

    // A Kenney / KayKit model scaled so its longest footprint side is `size`.
    GameObject Fit(string path, float size, Transform p, Vector3 pos, float yaw = 0, bool collider = false)
    {
        var go = Kit.Spawn(path, 1f, null);
        var b = Kit.WorldBounds(go);
        float s = size / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * s;
        if (collider) { var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, b.size.y * 0.5f, 0); bc.size = b.size; }
        return go;
    }

    void Collide(Vector3 center, Vector3 size) { var w = new GameObject("block"); w.transform.SetParent(world, false); w.transform.localPosition = center; var b = w.AddComponent<BoxCollider>(); b.size = size; }

    LineRenderer Outline(Vector3 min, Vector3 max, Color c, float width = 0.07f)
    {
        var go = new GameObject("outline"); go.transform.SetParent(world, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = Kit.UnlitAlpha; lr.loop = true; lr.positionCount = 4; lr.widthMultiplier = width; lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ; go.transform.rotation = Quaternion.Euler(90, 0, 0);
        lr.startColor = lr.endColor = c;
        lr.SetPositions(new[] { new Vector3(min.x, 0.03f, min.z), new Vector3(max.x, 0.03f, min.z), new Vector3(max.x, 0.03f, max.z), new Vector3(min.x, 0.03f, max.z) });
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    void Dashes(Vector3 a, Vector3 b, Color c, float dash = 0.55f, float gap = 0.45f)
    {
        var d = b - a; float len = d.magnitude; var dir = d / len;
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        for (float u = 0; u < len; u += dash + gap)
        {
            var q = Kit.FloorQuad("dash", null, c, 1f, world, a + dir * (u + dash * 0.5f), 0.022f);
            q.transform.localRotation = Quaternion.Euler(90, yaw, 0);
            q.transform.localScale = new Vector3(0.09f, Mathf.Min(dash, len - u), 1);
            q.sharedMaterial = LaneMat;
        }
    }
    static Material laneMat; Material LaneMat => laneMat ? laneMat : (laneMat = new Material(Kit.UnlitAlpha) { color = Kit.A(Kit.Hex("#ffd34d"), 0.75f) });

    void BuildWorld()
    {
        if (world) Destroy(world.gameObject);
        foreach (var w in Workers) if (w) Destroy(w.gameObject);
        Workers.Clear(); pads.Clear();
        world = new GameObject("World").transform;
        NavGrid.SetBounds(-14.8f, 14.8f, -10.8f, 10.6f);
        TerminalZone = null; Power = null;

        var d = D;
        Cam.backgroundColor = d.sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Kit.Hex("#8a9cc8") * 0.75f;
        RenderSettings.ambientEquatorColor = Kit.Hex("#5a6a90") * 0.6f;
        RenderSettings.ambientGroundColor = Kit.Hex("#1a2238") * 0.6f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = d.sky;
        RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 95f;

        BuildShell(d);
        BuildFloorPaint(d);
        BuildInbound(d);
        BuildBulk();
        BuildRack();
        BuildPackLine();
        BuildSorter();
        BuildOutbound(d);

        Player = Player.Create(world, new Vector3(-4.6f, 0, 0.6f));
        Hire(Worker.Job.DockCrew, false);

        double m = DepotMult;
        padDefs = new List<PadDef>
        {
            new PadDef { id = "power", cost = 30, pos = new Vector3(-5.2f, 0, -6.0f), label = "POWER PACK", build = BuildPower },
            new PadDef { id = "pally", cost = 60, pos = new Vector3(-7.2f, 0, 3.0f), label = "HIRE PALLY", build = () => Hire(Worker.Job.Pally, true) },
            new PadDef { id = "upgrades", cost = 90, pos = new Vector3(-0.8f, 0, -8.4f), label = "UPGRADES", build = BuildTerminal },
            new PadDef { id = "packer", cost = 140, pos = new Vector3(9.5f, 0, -4.1f), label = "HIRE PACKER", build = () => Hire(Worker.Job.Packer, true) },
            new PadDef { id = "picker", cost = 220, pos = new Vector3(1.8f, 0, 1.4f), label = "HIRE PICKER", build = () => Hire(Worker.Job.Picker, true) },
            new PadDef { id = "restock", cost = 320, pos = new Vector3(-7.4f, 0, -2.4f), label = "HIRE RESTOCKER", build = () => Hire(Worker.Job.Restocker, true) },
            new PadDef { id = "rack2", cost = 450, pos = new Vector3(2.6f, 0, 6.0f), label = "TALLER RACK", build = BuildRackLevel },
            new PadDef { id = "sorter", cost = 600, pos = new Vector3(14.0f, 0, -4.4f), label = "HIRE SORTER", build = () => Hire(Worker.Job.Sorter, true) },
            new PadDef { id = "forklift", cost = 800, pos = new Vector3(13.8f, 0, 3.0f), label = "HIRE FORKLIFT", build = () => Hire(Worker.Job.Forklift, true) },
            new PadDef { id = "crew2", cost = 1000, pos = new Vector3(-6.8f, 0, 7.2f), label = "BIGGER TRUCKS", build = () => Toast("BIGGER TRUCKS! More cartons, faster pallets") },
            new PadDef { id = "autobank", cost = 1300, pos = new Vector3(5.6f, 0, 5.6f), label = "AUTO-PAY", build = () => Toast("AUTO-PAY! Cash goes straight to you") },
            new PadDef { id = "express", cost = 1800, pos = new Vector3(-1.2f, 0, -3.6f), label = "EXPRESS ORDERS", build = () => Toast("EXPRESS ORDERS! More orders, +25% per box") },
            new PadDef { id = "newdepot", cost = 4000, pos = new Vector3(4.2f, 0, -9.6f), label = "OPEN " + Depots[(Save.depot + 1) % Depots.Length].name, build = NextDepot },
        };
        for (int i = 0; i < padDefs.Count; i++) { var p = padDefs[i]; p.cost *= m; padDefs[i] = p; }
        restoring = true;
        foreach (var pd in padDefs) if (Save.done.Contains(pd.id) && pd.id != "newdepot") pd.build();
        restoring = false;
        RefreshPads();
    }

    // ---------------------------------------------------------------- shell: deck, back wall with two dock doors, starfield
    void BuildShell(Depot d)
    {
        var floor = Box(world, new Vector3(0, -0.1f, 0), new Vector3(30f, 0.2f, 22f), null);
        var fm = new Material(Shader.Find("Standard")) { mainTexture = Kit.Tiles(64, d.floorA, d.floorB, d.line) };
        fm.mainTextureScale = new Vector2(30f / 2f, 22f / 2f); fm.SetFloat("_Glossiness", 0.35f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = fm;
        // deck edge
        var edge = M(ref trimMat, Kit.Hex("#2a3554"));
        Box(world, new Vector3(0, -0.35f, -11.1f), new Vector3(30.4f, 0.5f, 0.2f), edge);
        Box(world, new Vector3(-15.1f, -0.35f, 0), new Vector3(0.2f, 0.5f, 22.4f), edge);
        Box(world, new Vector3(15.1f, -0.35f, 0), new Vector3(0.2f, 0.5f, 22.4f), edge);

        // back wall: dark panels, posts, a light strip, two dock openings
        var wm = new Material(Shader.Find("Standard")) { color = d.wall }; wm.SetFloat("_Glossiness", 0.2f);
        var post = M(ref steel, Kit.Hex("#8ea0c4"), 0.5f);
        var strip = new Material(Shader.Find("Standard")) { color = Color.white }; strip.EnableKeyword("_EMISSION"); strip.SetColor("_EmissionColor", Kit.Hex("#dfe9ff") * 1.1f);
        float[] gaps = { -10.5f, 10.5f };
        for (float x = -15f; x < 15f; x += 0.5f)
        {
            bool inGap = false; foreach (var g in gaps) if (Mathf.Abs(x + 0.25f - g) < 2.2f) inGap = true;
            if (!inGap) Box(world, new Vector3(x + 0.25f, 2.1f, 11.25f), new Vector3(0.5f, 4.2f, 0.3f), wm);
        }
        foreach (var g in gaps) Box(world, new Vector3(g, 3.75f, 11.25f), new Vector3(4.4f, 0.9f, 0.3f), wm);   // lintel over each dock
        for (float x = -15f; x <= 15f; x += 3f) Box(world, new Vector3(x, 2.1f, 11.05f), new Vector3(0.22f, 4.2f, 0.22f), post);
        Box(world, new Vector3(0, 4.25f, 11.05f), new Vector3(30.2f, 0.18f, 0.3f), post);
        for (int i = 0; i < 4; i++) Box(world, new Vector3(-7.5f + i * 5f, 3.3f, 11.07f), new Vector3(2.6f, 0.12f, 0.05f), strip);
        // side rails
        for (float z = -10.5f; z <= 10.5f; z += 2f)
        {
            Box(world, new Vector3(-15f, 0.5f, z), new Vector3(0.12f, 1f, 0.12f), post);
            Box(world, new Vector3(15f, 0.5f, z), new Vector3(0.12f, 1f, 0.12f), post);
        }
        Box(world, new Vector3(-15f, 1f, 0), new Vector3(0.1f, 0.08f, 21.5f), post);
        Box(world, new Vector3(15f, 1f, 0), new Vector3(0.1f, 0.08f, 21.5f), post);

        // physics: floor + perimeter
        Collide(new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60));
        Collide(new Vector3(-15.3f, 1, 0), new Vector3(0.5f, 3, 24));
        Collide(new Vector3(15.3f, 1, 0), new Vector3(0.5f, 3, 24));
        Collide(new Vector3(0, 1, -11.3f), new Vector3(32, 3, 0.5f));
        Collide(new Vector3(0, 1, 11.3f), new Vector3(32, 3, 0.5f));

        // outside: landing pads, the void with stars
        var ground = Box(world, new Vector3(0, -0.6f, 24f), new Vector3(70f, 0.2f, 26f), null);
        var gm = new Material(Shader.Find("Standard")) { mainTexture = Kit.Noise(256, Kit.Hex("#30364a"), Kit.Hex("#49506a"), 0.04f, 7, 10) };
        gm.mainTextureScale = new Vector2(8, 3); gm.SetFloat("_Glossiness", 0.05f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = gm;
        Fit("Base/landingpad_large", 7f, world, new Vector3(-10.5f, -0.5f, 16f));
        Fit("Base/landingpad_large", 7f, world, new Vector3(10.5f, -0.5f, 16f));
        Fit("Base/basemodule_garage", 6f, world, new Vector3(0, -0.5f, 18f), 180);
        Fit("Base/structure_tall", 3f, world, new Vector3(-3.5f, -0.5f, 14.5f));
        Fit("Base/solarpanel", 3f, world, new Vector3(4f, -0.5f, 14.8f), 20);
        Fit("Base/containers_C", 3.2f, world, new Vector3(-17.5f, -0.5f, 14f), 90);
        Fit("Base/containers_D", 3.2f, world, new Vector3(17.5f, -0.5f, 13.5f), -90);
        Fit("Base/rocks_A", 4f, world, new Vector3(-19f, -0.5f, 20f));
        Fit("Base/lander_B", 4f, world, new Vector3(16f, -0.5f, 21f), 30);
        var stars = new GameObject("stars").transform; stars.SetParent(world, false);
        var rng = new System.Random(11);
        var starMat = new Material(Kit.UnlitAlpha) { mainTexture = Kit.Glow, color = Color.white };
        for (int i = 0; i < 90; i++)
        {
            var q = Kit.FloorQuad("star", Kit.Glow, Color.white, 0.25f + (float)rng.NextDouble() * 0.5f, stars, new Vector3(-45 + (float)rng.NextDouble() * 90, 12f + (float)rng.NextDouble() * 22f, 40f + (float)rng.NextDouble() * 10f), 0);
            q.transform.localRotation = Quaternion.Euler(0, 0, 0);
            q.sharedMaterial = starMat;
        }
    }

    // ---------------------------------------------------------------- painted floor: lanes, logo
    void BuildFloorPaint(Depot d)
    {
        var faint = new Color(1, 1, 1, 0.13f);
        FloorText.Make(world, new Vector3(-1.2f, 0, -4.9f), "ORBIT DEPOT", 150, new Color(1, 1, 1, 0.16f), 0, 2400);
        FloorText.Make(world, new Vector3(-1.2f, 0, -6.1f), "FULFILLMENT FLOOR", 64, new Color(1, 1, 1, 0.12f), 0, 2400);
        // lanes
        Dashes(new Vector3(-6.5f, 0, -10.4f), new Vector3(-6.5f, 0, 10.2f), Color.white);
        Dashes(new Vector3(1.9f, 0, 2.2f), new Vector3(1.9f, 0, 10.2f), Color.white);
        Dashes(new Vector3(-6.3f, 0, 2.6f), new Vector3(14.6f, 0, 2.6f), Color.white);
        Dashes(new Vector3(-6.3f, 0, -0.2f), new Vector3(1.6f, 0, -0.2f), Color.white);
        Dashes(new Vector3(1.9f, 0, -5.0f), new Vector3(14.6f, 0, -5.0f), Color.white);
        FloorText.Make(world, new Vector3(10.6f, 0, 8.6f), "FORKLIFT / PALLY", 54, faint, 0, 900);
        for (int i = 0; i < 3; i++) FloorText.Make(world, new Vector3(10.6f, 0, 9.4f + i * 0.45f), "^", 80, faint, 0, 200);
    }

    // ---------------------------------------------------------------- 01 inbound dock
    void BuildInbound(Depot d)
    {
        var root = new GameObject("Inbound").transform; root.SetParent(world, false);
        var dock = root.gameObject.AddComponent<InboundDock>();
        var yellow = Kit.Hex("#ffc93c");
        Outline(new Vector3(-14.4f, 0, 5.2f), new Vector3(-7.2f, 0, 10.8f), Kit.A(yellow, 0.9f));
        // hazard door posts
        Hazard(new Vector3(-12.6f, 0, 11.0f)); Hazard(new Vector3(-8.4f, 0, 11.0f));

        var loose = new GameObject("Loose"); loose.transform.SetParent(root, false); loose.transform.localPosition = new Vector3(-10.5f, 0, 9.8f);
        dock.Loose = loose.AddComponent<ItemStack>();
        dock.Loose.Cols = 4; dock.Loose.Rows = 3; dock.Loose.Cell = new Vector2(0.56f, 0.5f); dock.Loose.Capacity = 12; dock.Loose.FlyTime = 0.32f; dock.Loose.ArcHeight = 1.2f;
        var ready = new GameObject("Ready"); ready.transform.SetParent(root, false); ready.transform.localPosition = new Vector3(-11.6f, 0, 6.8f);
        dock.Ready = ready.AddComponent<ItemStack>();
        dock.Ready.Cols = 4; dock.Ready.Rows = 1; dock.Ready.Cell = new Vector2(1.18f, 1.2f); dock.Ready.Capacity = 4; dock.Ready.Bulk = true; dock.Ready.FlyTime = 0.35f; dock.Ready.ArcHeight = 0.4f;
        var bs = new GameObject("BuildSpot"); bs.transform.SetParent(root, false); bs.transform.localPosition = new Vector3(-8.3f, 0, 8.4f);
        dock.BuildSpot = bs.transform;
        Collide(new Vector3(-11.6f, 0.5f, 6.8f), new Vector3(4.6f, 1f, 1.1f));
        Collide(new Vector3(-8.3f, 0.5f, 8.4f), new Vector3(1.1f, 1f, 1.1f));

        dock.Zone = Zone.Make(world, new Vector3(-10.4f, 0, 4.3f), 1.05f, yellow, "z01", "01");
        dock.Header = Sign.Make(world, new Vector3(-10.5f, 3.05f, 10.6f), 4.6f, yellow, "INBOUND · DOCK 01", Sign.Style.Header);
        dock.BuildSign = Sign.Make(world, new Vector3(-8.0f, 2.3f, 9.2f), 3.2f, yellow, "ELI · DOCK CREW");
        dock.ReadySign = Sign.Make(world, new Vector3(-11.4f, 1.15f, 5.3f), 4.2f, yellow, "01  PALLETS READY");
        FloorText.Make(world, new Vector3(-8.3f, 0, 7.4f), "BUILD PALLET", 30, new Color(1, 1, 1, 0.2f), 0, 500);

        // the space truck that brings cartons
        var truck = Fit("Base/spacetruck_large", 3.4f, world, Vector3.zero, 180).transform;
        dock.Truck = truck;
        dock.TruckDock = new Vector3(-10.5f, 0, 12.9f); dock.TruckAway = new Vector3(-10.5f, 0, 28f);
        truck.position = dock.TruckAway; truck.gameObject.SetActive(false);
        Dock = dock;
    }

    void Hazard(Vector3 at)
    {
        var y = M(ref rackOrange, Kit.Hex("#ffb000"), 0.4f);
        var k = new Material(Shader.Find("Standard")) { color = Kit.Hex("#14161c") };
        for (int i = 0; i < 8; i++) Box(world, at + new Vector3(0, 0.25f + i * 0.5f, 0), new Vector3(0.32f, 0.5f, 0.32f), i % 2 == 0 ? y : k);
    }

    // ---------------------------------------------------------------- 02 bulk storage
    void BuildBulk()
    {
        var root = new GameObject("Bulk").transform; root.SetParent(world, false);
        var bulk = root.gameObject.AddComponent<BulkStorage>();
        var blue = Kit.Hex("#3cc7ff");
        Outline(new Vector3(-14.4f, 0, -2.0f), new Vector3(-7.4f, 0, 2.3f), Kit.A(blue, 0.9f));
        var slots = new GameObject("Slots"); slots.transform.SetParent(root, false); slots.transform.localPosition = new Vector3(-10.9f, 0, 0.55f);
        bulk.Slots = slots.AddComponent<ItemStack>();
        bulk.Slots.Cols = 3; bulk.Slots.Rows = 2; bulk.Slots.Cell = new Vector2(1.4f, 1.35f); bulk.Slots.Capacity = 6; bulk.Slots.Bulk = true; bulk.Slots.FlyTime = 0.4f; bulk.Slots.ArcHeight = 0.5f;
        // floor bays + end posts
        var post = M(ref rackBlue, Kit.Hex("#3d7bff"), 0.45f);
        foreach (var x in new[] { -13.2f, -8.6f }) foreach (var z in new[] { -0.9f, 2.0f }) Box(root, new Vector3(x, 0.45f, z), new Vector3(0.12f, 0.9f, 0.12f), M(ref rackOrange, Kit.Hex("#ffb000"), 0.4f));
        FloorText.Make(world, new Vector3(-10.9f, 0, 2.0f), "BULK PALLETS", 56, new Color(1, 1, 1, 0.2f), 0, 900);
        Collide(new Vector3(-10.9f, 0.5f, 0.55f), new Vector3(4.3f, 1f, 2.8f));
        bulk.Zone = Zone.Make(world, new Vector3(-10.9f, 0, -3.9f), 1.05f, blue, "z02", "02");
        bulk.Card = Sign.Make(world, new Vector3(-10.9f, 1.2f, -2.2f), 4.2f, blue, "02  BULK STORAGE");
        Bulk = bulk;
    }

    // ---------------------------------------------------------------- 03/04 pick rack
    Transform rackRoot;
    void BuildRack()
    {
        var root = new GameObject("Rack").transform; root.SetParent(world, false); root.localPosition = new Vector3(-2.2f, 0, 4.6f);
        rackRoot = root;
        var rack = root.gameObject.AddComponent<PickRack>();
        Rack = rack;
        RackFrame(2);
        Collide(new Vector3(-2.2f, 0.8f, 4.6f), new Vector3(6.4f, 1.6f, 1.4f));
        rack.Back = Zone.Make(world, new Vector3(-2.2f, 0, 6.6f), 1.0f, Kit.Hex("#9da8ff"), "z03", "03", "RESTOCK");
        rack.Front = Zone.Make(world, new Vector3(-2.2f, 0, 2.5f), 1.0f, Kit.Hex("#c792ff"), "z04", "04", "PICK FRONT");
        rack.BackCard = Sign.Make(world, new Vector3(2.4f, 2.3f, 8.4f), 3.8f, Kit.Hex("#9da8ff"), "03  RESTOCK · BACK");
        rack.FrontCard = Sign.Make(world, new Vector3(-2.2f, 2.6f, 3.4f), 4.2f, Kit.Hex("#c792ff"), "04  PICK · FRONT");
        FloorText.Make(world, new Vector3(-2.2f, 0, 1.15f), "PICK", 70, new Color(1, 1, 1, 0.2f), 0, 600);
        FloorText.Make(world, new Vector3(-2.2f, 0, 7.75f), "RESTOCK", 50, new Color(1, 1, 1, 0.16f), 0, 600);
    }

    // posts + beams + shelves; levels 2 (32 slots) or 3 (48)
    void RackFrame(int levels)
    {
        var root = rackRoot;
        foreach (Transform c in root) Destroy(c.gameObject);
        Rack.SlotBoxes.Clear();
        var blue = M(ref rackBlue, Kit.Hex("#3d7bff"), 0.45f);
        var orange = M(ref rackOrange, Kit.Hex("#ffb000"), 0.4f);
        var sh = M(ref shelf, Kit.Hex("#c7cfdf"), 0.3f);
        float top = 0.25f + levels * 0.75f;
        for (int i = 0; i < 4; i++)
            foreach (var z in new[] { -0.55f, 0.55f })
                Box(root, new Vector3(-3f + i * 2f, top * 0.5f, z), new Vector3(0.09f, top, 0.09f), blue);
        for (int l = 0; l < levels; l++)
        {
            float y = 0.3f + l * 0.75f;
            Box(root, new Vector3(0, y, 0), new Vector3(6.1f, 0.04f, 1.12f), sh);
            Box(root, new Vector3(0, y + 0.03f, -0.57f), new Vector3(6.1f, 0.08f, 0.05f), orange);
            Box(root, new Vector3(0, y + 0.03f, 0.57f), new Vector3(6.1f, 0.08f, 0.05f), orange);
            for (int c = 0; c < 8; c++)
                for (int r = 0; r < 2; r++)
                {
                    var b = ItemPool.Model("Furniture/cardboardBoxClosed", 0.5f, root);
                    b.localPosition = new Vector3(-2.65f + c * 0.76f, y + 0.02f, r == 0 ? -0.26f : 0.26f);
                    b.gameObject.SetActive(false);
                    Rack.SlotBoxes.Add(b.gameObject);
                }
        }
        Rack.Cap = levels * 16;
    }

    void BuildRackLevel()
    {
        RackFrame(3);
        if (!restoring) { Burst(rackRoot.position, Kit.Hex("#ffc93c")); Toast("TALLER RACK! 48 slots"); }
    }

    // ---------------------------------------------------------------- 05 pack line
    void BuildPackLine()
    {
        var root = new GameObject("PackLine").transform; root.SetParent(world, false);
        var pl = root.gameObject.AddComponent<PackLine>();
        float z = -1.6f;
        Outline(new Vector3(1.3f, 0, -2.5f), new Vector3(13.3f, 0, -0.6f), new Color(1, 1, 1, 0.55f));
        // conveyors: Kenney factory belts tiled along x
        for (float x = 1.9f; x < 6.2f; x += 0.95f) Fit("Factory/conveyor-stripe", 0.95f, root, new Vector3(x, 0, z), 90);
        for (float x = 8.25f; x < 11.0f; x += 0.95f) Fit("Factory/conveyor", 0.95f, root, new Vector3(x, 0, z), 90);
        float beltY = BeltTop(root);
        pl.InA = new Vector3(1.9f, beltY, z); pl.InB = new Vector3(6f, beltY, z);
        pl.OutA = new Vector3(8.2f, beltY, z); pl.OutB = new Vector3(10.8f, beltY, z);
        // packing table + screen + scanner
        var steelM = M(ref steel, Kit.Hex("#8ea0c4"), 0.5f);
        Box(root, new Vector3(7.15f, 0.42f, z), new Vector3(1.6f, 0.84f, 1.0f), steelM);
        var top = Box(root, new Vector3(7.15f, 0.86f, z), new Vector3(1.7f, 0.06f, 1.1f), M(ref shelf, Kit.Hex("#c7cfdf"), 0.3f));
        var scr = Fit("Factory/screen-flat", 0.7f, root, new Vector3(7.5f, 0.89f, z + 0.35f), 180).transform;
        pl.Screen = scr;
        Fit("Factory/scanner-low", 0.6f, root, new Vector3(6.75f, 0.89f, z + 0.3f), 180);
        // cart
        var cartRoot = new GameObject("Cart").transform; cartRoot.SetParent(root, false); cartRoot.localPosition = new Vector3(11.9f, 0, z);
        Box(cartRoot, new Vector3(0, 0.32f, 0), new Vector3(1.05f, 0.05f, 1.05f), steelM);
        foreach (var cx in new[] { -0.5f, 0.5f }) foreach (var cz in new[] { -0.5f, 0.5f }) Box(cartRoot, new Vector3(cx, 0.6f, cz), new Vector3(0.04f, 0.6f, 0.04f), steelM);
        Box(cartRoot, new Vector3(0, 0.9f, 0.5f), new Vector3(1.05f, 0.04f, 0.04f), steelM);
        foreach (var cx in new[] { -0.42f, 0.42f }) foreach (var cz in new[] { -0.42f, 0.42f }) Box(cartRoot, new Vector3(cx, 0.08f, cz), new Vector3(0.12f, 0.16f, 0.12f), M(ref trimMat, Kit.Hex("#2a3554")));
        var cs = new GameObject("CartStack"); cs.transform.SetParent(cartRoot, false); cs.transform.localPosition = new Vector3(0, 0.35f, 0);
        pl.Cart = cs.AddComponent<ItemStack>();
        pl.Cart.Cols = 2; pl.Cart.Rows = 2; pl.Cart.Cell = new Vector2(0.46f, 0.46f); pl.Cart.Capacity = 12; pl.Cart.FlyTime = 0.25f; pl.Cart.ArcHeight = 0.5f;
        Collide(new Vector3(6.95f, 0.5f, z), new Vector3(11.0f, 1f, 1.1f));
        Collide(new Vector3(11.9f, 0.5f, z), new Vector3(1.1f, 1f, 1.1f));

        var white = Kit.Hex("#e8eefc");
        pl.DropZone = Zone.Make(world, new Vector3(2.9f, 0, -3.5f), 0.95f, Kit.Hex("#7fd4ff"), "z05a", "05A", "DROP BOXES");
        pl.PackZone = Zone.Make(world, new Vector3(7.15f, 0, -3.5f), 0.95f, Kit.Hex("#ffe066"), "z05b", "05B", "PACK BOXES");
        pl.CartZone = Zone.Make(world, new Vector3(11.9f, 0, -3.5f), 0.95f, Kit.Hex("#ffb35c"), "z05c", "05C", "TAKE CART");
        pl.DropCard = Sign.Make(world, new Vector3(2.6f, 1.05f, -5.3f), 3.1f, Kit.Hex("#7fd4ff"), "05A  DROP BOXES");
        pl.PackCard = Sign.Make(world, new Vector3(6.9f, 1.05f, -5.3f), 3.1f, Kit.Hex("#ffe066"), "05B  PACK BOXES");
        pl.CartCard = Sign.Make(world, new Vector3(11.2f, 1.05f, -5.3f), 3.1f, Kit.Hex("#ffb35c"), "05C  TAKE CART");
        Sign.Make(world, new Vector3(4f, 1.45f, -0.7f), 2.4f, white, "INPUT CONVEYOR", Sign.Style.Label);
        Sign.Make(world, new Vector3(7.2f, 1.95f, -0.75f), 2.2f, white, "PACKING TABLE", Sign.Style.Label);
        pl.CartLabel = Sign.Make(world, new Vector3(11.9f, 1.75f, -0.75f), 1.8f, white, "CART 0/12", Sign.Style.Label);
        pl.NeedsPacker = Sign.Make(world, new Vector3(7.2f, 2.75f, 0.1f), 3.4f, white, "NEEDS A PACKER", Sign.Style.Callout);
        pl.NeedsPacker.Visible = false;
        FloorText.Make(world, new Vector3(7.4f, 0, -2.78f), "DROP  >  PACK  >  TAKE CART", 36, new Color(1, 1, 1, 0.18f), 0, 1400);
        Pack = pl;
    }

    float BeltTop(Transform root)
    {
        float y = 0.45f;
        foreach (var r in root.GetComponentsInChildren<Renderer>()) y = Mathf.Max(y, r.bounds.max.y);
        return Mathf.Clamp(y, 0.3f, 0.9f) + 0.02f;
    }

    // ---------------------------------------------------------------- 06 sorting
    void BuildSorter()
    {
        var root = new GameObject("Sorter").transform; root.SetParent(world, false);
        var so = root.gameObject.AddComponent<Sorter>();
        var pink = Kit.Hex("#ff7ab8");
        Outline(new Vector3(2.3f, 0, -10.5f), new Vector3(14.3f, 0, -6.2f), Kit.A(pink, 0.8f));
        var hop = Fit("Factory/hopper-square", 1.3f, root, new Vector3(3.0f, 0, -8.0f), 0, true);
        var chute = new GameObject("Chute"); chute.transform.SetParent(root, false);
        chute.transform.localPosition = new Vector3(3.0f, Kit.WorldBounds(hop).max.y, -8.0f);
        so.Chute = chute.AddComponent<ItemStack>();
        so.Chute.Cols = 2; so.Chute.Rows = 2; so.Chute.Cell = new Vector2(0.4f, 0.4f); so.Chute.Capacity = 24; so.Chute.FlyTime = 0.25f;
        var arm = Fit("Factory/robot-arm-a", 1.3f, root, new Vector3(4.7f, 0, -8.1f), 0, true);
        so.Arm = arm.transform.GetChild(0);
        string[] names = { "A", "B", "C", "D" };
        for (int i = 0; i < 4; i++)
        {
            var slot = new GameObject("Bin" + names[i]).transform; slot.SetParent(root, false); slot.localPosition = new Vector3(6.5f + i * 1.75f, 0, -8.9f);
            so.BinSlots[i] = slot;
            so.BinLabels[i] = Sign.Make(world, new Vector3(6.5f + i * 1.75f, 1.6f, -9.9f), 1.6f, pink, "BIN " + names[i], Sign.Style.Label);
        }
        Collide(new Vector3(9.1f, 0.5f, -8.9f), new Vector3(7.2f, 1f, 1.2f));
        so.DropZone = Zone.Make(world, new Vector3(3.0f, 0, -6.2f), 0.95f, pink, "z06a", "06A", "DROP · SORT");
        so.LiftZone = Zone.Make(world, new Vector3(13.2f, 0, -7.4f), 0.95f, Kit.Hex("#ffb35c"), "z06b", "06B", "FORKLIFT");
        so.DropCard = Sign.Make(world, new Vector3(3.6f, 2.5f, -9.2f), 3.1f, pink, "06A  DROP + SORT");
        so.LiftCard = Sign.Make(world, new Vector3(12.6f, 2.5f, -9.6f), 3.0f, Kit.Hex("#ffb35c"), "06B  FORKLIFT");
        so.BringCart = Sign.Make(world, new Vector3(7.5f, 2.9f, -6.6f), 4.6f, Color.white, "BRING A CART FROM 05", Sign.Style.Callout);
        so.BringCart.Visible = false;
        FloorText.Make(world, new Vector3(8.4f, 0, -10.6f), "CARTS IN  >  SORT  >  BINS", 46, new Color(1, 1, 1, 0.18f), 0, 1400);
        Sort = so;
    }

    // ---------------------------------------------------------------- 07 outbound dock + cash
    void BuildOutbound(Depot d)
    {
        var root = new GameObject("Outbound").transform; root.SetParent(world, false);
        var ob = root.gameObject.AddComponent<OutboundDock>();
        var green = Kit.Hex("#6dff9a");
        Outline(new Vector3(8.2f, 0, 5.2f), new Vector3(13.4f, 0, 10.8f), Kit.A(green, 0.85f));
        Hazard(new Vector3(8.4f, 0, 11.0f)); Hazard(new Vector3(12.6f, 0, 11.0f));
        // the dock leveller where bins go out
        var bay = new GameObject("Bay").transform; bay.SetParent(root, false); bay.localPosition = new Vector3(10.5f, 0, 10.6f);
        Box(root, new Vector3(10.5f, 0.02f, 10.2f), new Vector3(3.8f, 0.04f, 1.4f), M(ref trimMat, Kit.Hex("#2a3554")));
        ob.Bay = bay;
        ob.Zone = Zone.Make(world, new Vector3(10.5f, 0, 6.2f), 1.05f, green, "z07", "07", "SHIP IT");
        ob.Header = Sign.Make(world, new Vector3(10.5f, 3.05f, 10.6f), 4.6f, green, "OUTBOUND · DOCK 07", Sign.Style.Header);
        ob.Header.Set("BIN OUT  =  GET PAID");
        ob.Card = Sign.Make(world, new Vector3(11.2f, 1.15f, 4.9f), 4.2f, green, "07  OUTBOUND · GET PAID");
        var truck = Fit("Base/spacetruck_large", 3.4f, world, Vector3.zero, 180).transform;
        ob.Truck = truck; ob.TruckDock = new Vector3(10.5f, 0, 12.9f); ob.TruckAway = new Vector3(10.5f, 0, 28f);
        truck.position = ob.TruckDock;
        // cash pile
        var cashRoot = new GameObject("Cash"); cashRoot.transform.SetParent(world, false); cashRoot.transform.localPosition = new Vector3(6.9f, 0, 7.4f);
        var cp = cashRoot.AddComponent<CashPile>();
        Box(cashRoot.transform, new Vector3(0, 0.3f, 0), new Vector3(1.3f, 0.6f, 0.9f), M(ref steel, Kit.Hex("#8ea0c4"), 0.5f), true);
        var pile = new GameObject("Pile"); pile.transform.SetParent(cashRoot.transform, false); pile.transform.localPosition = new Vector3(0, 0.6f, 0);
        cp.Pile = pile.AddComponent<ItemStack>();
        cp.Pile.Cols = 3; cp.Pile.Rows = 2; cp.Pile.Cell = new Vector2(0.44f, 0.26f); cp.Pile.FlyTime = 0.45f; cp.Pile.ArcHeight = 1.6f;
        cp.Zone = Zone.Make(world, new Vector3(6.9f, 0, 5.9f), 0.9f, Kit.Hex("#8cff9e"), "zcash", "$", "GET PAID");
        ob.Cash = cp;
        Out = ob;
    }

    // ---------------------------------------------------------------- unlockables
    void BuildPower()
    {
        var root = new GameObject("PowerPack").transform; root.SetParent(world, false); root.localPosition = new Vector3(-5.2f, 0, -7.5f);
        var pp = root.gameObject.AddComponent<PowerPack>();
        var pink = Kit.Hex("#ff5c9a");
        var desk = Box(root, new Vector3(0, 0.42f, 0), new Vector3(2.2f, 0.84f, 0.9f), M(ref steel, Kit.Hex("#8ea0c4"), 0.5f), true);
        Fit("Factory/screen-panel-wide", 1.0f, root, new Vector3(0.2f, 0.86f, 0.25f), 180);
        Fit("Factory/box-small", 0.4f, root, new Vector3(-0.7f, 0.86f, 0f));
        var tote = ItemPool.Get(Item.Tote); tote.SetParent(root, false); tote.localPosition = new Vector3(-0.75f, 0.86f, -0.05f);
        Outline(root.position + new Vector3(-1.6f, 0, -1.3f), root.position + new Vector3(1.6f, 0, 2.6f), Kit.A(pink, 0.85f));
        pp.Zone = Zone.Make(world, new Vector3(-5.2f, 0, -6.0f), 0.95f, pink, "zpower", "2X", "POWER PACK");
        pp.Card = Sign.Make(world, new Vector3(-5.2f, 1.05f, -8.9f), 3.6f, pink, "POWER PACK");
        Power = pp;
        if (!restoring) { StartCoroutine(PopIn(root)); Toast("POWER PACK! Stand on it for 2× packing + 2× pay"); }
    }

    void BuildTerminal()
    {
        var root = new GameObject("Terminal"); root.transform.SetParent(world, false); root.transform.localPosition = new Vector3(-0.8f, 0, -9.6f);
        Box(root.transform, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.7f), M(ref steel, Kit.Hex("#8ea0c4"), 0.5f), true);
        Fit("Factory/screen-flat", 0.9f, root.transform, new Vector3(0, 0.9f, 0.1f), 180);
        var term = root.AddComponent<Terminal>();
        term.Zone = Zone.Make(world, new Vector3(-0.8f, 0, -8.3f), 0.85f, Kit.Hex("#7fd4ff"), "zup", "UP", "UPGRADES");
        TerminalZone = term.Zone;
        Sign.Make(world, new Vector3(-0.8f, 2.0f, -9.9f), 2.4f, Kit.Hex("#7fd4ff"), "UPGRADES", Sign.Style.Label);
        if (!restoring) StartCoroutine(PopIn(root.transform));
    }

    void Hire(Worker.Job job, bool announce)
    {
        Vector3 home = job switch
        {
            Worker.Job.Pally => new Vector3(-7.6f, 0, 4.4f),
            Worker.Job.Restocker => new Vector3(-7.4f, 0, -0.8f),
            Worker.Job.Picker => new Vector3(0.6f, 0, 1.6f),
            Worker.Job.Packer => new Vector3(7.15f, 0, -0.6f),
            Worker.Job.Sorter => new Vector3(14.0f, 0, -3.2f),
            Worker.Job.Forklift => new Vector3(14.0f, 0, 1.4f),
            _ => new Vector3(-8.3f, 0, 9.5f),
        };
        var w = Worker.Create(world, job, home);
        Workers.Add(w);
        if (job == Worker.Job.DockCrew) Dock.Crew = w.transform;
        if (job == Worker.Job.Packer) w.transform.rotation = Quaternion.Euler(0, 180, 0);
        if (announce && !restoring) { StartCoroutine(PopIn(w.transform)); Toast(w.Name + " HIRED! " + Worker.Title(job) + " is handled."); }
    }

    public bool HasJob(Worker.Job j) => Workers.Exists(w => w && w.Kind == j);

    // ---------------------------------------------------------------- pads
    void RefreshPads()
    {
        if (Save.tut < (int)TutStep.Buy) return;   // the first loop is guided; building opens up after your first payday
        int shown = 0;
        foreach (var d in padDefs)
        {
            if (Save.done.Contains(d.id)) continue;
            if (shown++ >= 2) break;
            if (pads.Exists(x => x && x.Id == d.id)) continue;
            var go = new GameObject("pad_" + d.id);
            go.transform.SetParent(world, false);
            go.transform.localPosition = d.pos;
            var pad = go.AddComponent<Pad>();
            pad.Id = d.id; pad.Cost = d.cost; pad.Paid = PadPaid(d.id); pad.Label = d.label;
            var dd = d; pad.OnDone = dd.build;
            pad.Zone = Zone.Make(go.transform, Vector3.zero, 0.95f, D.accent, "padzone");
            FloorText.Make(go.transform, new Vector3(0, 0, 0), "+", 90, Kit.A(D.accent, 0.5f), 0, 200);
            pad.Tag = UI.I.Tag(go.transform, Vector3.up * 0.4f, false);
            pads.Add(pad);
            if (!restoring) StartCoroutine(PopIn(go.transform));
        }
    }

    public Pad AffordablePad() { foreach (var p in pads) if (p && Money >= p.Cost - p.Paid) return p; return null; }

    public void CompletePad(Pad pad)
    {
        Save.done.Add(pad.Id);
        pads.Remove(pad);
        pad.Tag.Destroy();
        var pos = pad.transform.position;
        Destroy(pad.gameObject);
        Sfx.I.Unlock();
        Burst(pos, D.accent);
        pad.OnDone?.Invoke();
        Tutorial(TutStep.Buy);
        NavGrid.MarkDirty();
        WebBridge.Event("unlock_" + pad.Id, (int)Math.Min(int.MaxValue, pad.Cost));
        RefreshPads();
        Persist();
    }

    // ---------------------------------------------------------------- next depot (prestige)
    void NextDepot()
    {
        if (restoring) return;
        StartCoroutine(NextDepotSeq());
    }

    IEnumerator NextDepotSeq()
    {
        launching = true;
        WebBridge.Event("new_depot", Save.depot + 1);
        UI.I.FloatText(Player.transform.position + Vector3.up * 3f, "OPENING " + Depots[(Save.depot + 1) % Depots.Length].name + "!", Color.white, 60);
        Sfx.I.Whoosh();
        float t = 0;
        while (t < 1.6f) { t += Time.deltaTime; Cam.transform.position += Vector3.up * Time.deltaTime * 14f; yield return null; }
        Save.depot++; Save.opened++;
        Save.money = 0; Save.done.Clear(); Save.padIds.Clear(); Save.padPaid.Clear();
        Save.lvSpeed = Save.lvCap = Save.lvProfit = Save.lvStaff = 0;
        Save.quest = 0; Save.pShipped = 0; Save.pEarned = 0;
        Save.ready.Clear(); Save.bulk.Clear(); Save.bins.Clear(); Save.hands.Clear();
        Save.loose = Save.rackStock = Save.orders = Save.chute = Save.cart = Save.infeed = Save.outfeed = 0; Save.cashPile = 0;
        launching = false;
        Pack.Clear();
        BuildWorld();
        hadSave = false; RestoreState(); hadSave = true;
        Persist();
        Sfx.I.Unlock();
        UI.I.Confirm("WELCOME TO " + D.name + "!", "A brand-new floor.\nEvery box ships for 4× more here.", "LET'S SHIP!", null);
    }

    // ======================================================================
    // Quests
    struct Quest { public string text; public Func<double> value; public double goal; public double reward; }
    List<Quest> quests;
    public string QuestText; public float QuestProgress; public bool QuestVisible;

    List<Quest> Quests() => quests ??= new List<Quest>
    {
        new Quest { text = "Ship {0} boxes", value = () => Save.pShipped, goal = 16, reward = 40 },
        new Quest { text = "Hire {0} crew", value = () => StaffCount, goal = 2, reward = 80 },
        new Quest { text = "Earn {0}", value = () => Save.pEarned, goal = 600, reward = 120 },
        new Quest { text = "Ship {0} boxes", value = () => Save.pShipped, goal = 150, reward = 300 },
        new Quest { text = "Hire {0} crew", value = () => StaffCount, goal = 5, reward = 500 },
        new Quest { text = "Ship {0} boxes", value = () => Save.pShipped, goal = 600, reward = 900 },
        new Quest { text = "Earn {0}", value = () => Save.pEarned, goal = 12000, reward = 2000 },
    };

    void UpdateQuest()
    {
        var q = Quests();
        if (Save.quest >= q.Count || Save.tut < (int)TutStep.Buy) { QuestVisible = false; return; }
        var cur = q[Save.quest];
        bool money = cur.text.StartsWith("Earn");
        double goal = cur.goal * (money ? DepotMult : 1);
        double v = cur.value();
        QuestVisible = true;
        QuestText = string.Format(cur.text, money ? Kit.Money(goal) : goal.ToString());
        QuestProgress = (float)Math.Min(1, v / goal);
        if (v >= goal)
        {
            double reward = cur.reward * DepotMult;
            Save.quest++;
            Out.Cash.Deposit(reward, Player.transform.position + Vector3.up * 3f);
            Sfx.I.LevelUp();
            UI.I.FloatText(Player.transform.position + Vector3.up * 2.8f, "QUEST COMPLETE!  +" + Kit.Money(reward), Kit.Hex("#FFC93C"), 52);
            UI.I.PunchQuest();
            WebBridge.Event("quest_" + Save.quest);
            dirty = true;
        }
    }

    // ======================================================================
    // Orders + peak season
    void UpdateOrders(float dt)
    {
        float gap = 2.6f / (1f + 0.12f * StaffCount) / (Express ? 1.5f : 1f) / (RushLeft > 0 ? 2f : 1f);
        orderT -= dt;
        if (orderT <= 0 && Orders < 40) { orderT = gap * UnityEngine.Random.Range(0.8f, 1.2f); Orders++; }

        if (Save.tut < (int)TutStep.Done || launching) return;
        if (RushLeft > 0) { RushLeft -= dt; if (RushLeft <= 0) { RushLeft = 0; Sfx.I.RushEnd(); } return; }
        rushT -= dt;
        if (rushT <= 0 && StaffCount >= 2)
        {
            rushT = UnityEngine.Random.Range(160f, 230f);
            RushLeft = 45f;
            Sfx.I.Rush();
            UI.I.FloatText(Player.transform.position + Vector3.up * 3f, "PEAK SEASON!  2× ORDERS · 1.5× PAY", Kit.Hex("#FF7A59"), 56);
            WebBridge.Event("peak_season");
        }
    }

    // ======================================================================
    // Tutorial + guidance
    public void Tutorial(TutStep s)
    {
        if (Save.tut != (int)s) return;
        Save.tut++;
        dirty = true;
        WebBridge.Event("tut_" + s.ToString().ToLower());
        if (Save.tut == (int)TutStep.Buy) RefreshPads();
        if (Save.tut == (int)TutStep.Done) { Toast("YOU RUN THIS FLOOR NOW! Keep expanding."); Persist(); }
    }

    void UpdateGuidance()
    {
        ArrowTarget = null; Hint = null;
        var step = (TutStep)Save.tut;
        var top = Player.Stack.Empty ? (Item?)null : Player.Stack.TopType;
        switch (step)
        {
            case TutStep.Move: Hint = "Drag anywhere to move"; break;
            case TutStep.TakePallet: Hint = Dock.Ready.Count > 0 ? "Grab a pallet at INBOUND 01" : "The dock crew is building a pallet..."; ArrowTarget = Dock.Zone.transform.position; break;
            case TutStep.StorePallet: Hint = "Store the pallet in BULK STORAGE 02"; ArrowTarget = Bulk.Zone.transform.position; break;
            case TutStep.GrabCartons: Hint = "Break it down: grab cartons at 02"; ArrowTarget = Bulk.Zone.transform.position; break;
            case TutStep.Restock: Hint = "Restock the rack from the back (03)"; ArrowTarget = Rack.Back.transform.position; break;
            case TutStep.Pick: Hint = "Pick orders at the rack front (04)"; ArrowTarget = Rack.Front.transform.position; break;
            case TutStep.DropTotes: Hint = "Drop totes on the infeed (05A)"; ArrowTarget = Pack.DropZone.transform.position; break;
            case TutStep.Pack: Hint = "Stand at the packing table to pack (05B)"; ArrowTarget = Pack.PackZone.transform.position; break;
            case TutStep.TakeParcels: Hint = "Take the packed boxes from the cart (05C)"; ArrowTarget = Pack.CartZone.transform.position; break;
            case TutStep.Sort: Hint = "Drop boxes at the sorter (06A)"; ArrowTarget = Sort.DropZone.transform.position; break;
            case TutStep.TakeBin: Hint = "Forklift a bin at 06B"; ArrowTarget = Sort.LiftZone.transform.position; break;
            case TutStep.Ship: Hint = "Ship the bin at OUTBOUND 07"; ArrowTarget = Out.Zone.transform.position; break;
            case TutStep.Collect: Hint = "Get paid!"; ArrowTarget = Out.Cash.Zone.transform.position; break;
            case TutStep.Buy:
                var pad = pads.Count > 0 ? pads[0] : null;
                if (pad) { Hint = "Stand on the pad to build"; ArrowTarget = pad.transform.position; }
                break;
            default:
                var afford = AffordablePad();
                if (afford) ArrowTarget = afford.transform.position;
                else if (top == Item.Gaylord) { Hint = "Ship it at 07"; ArrowTarget = Out.Zone.transform.position; }
                else if (Rack.Stock == 0 && Orders > 5 && !HasJob(Worker.Job.Restocker)) { Hint = "Rack's empty! Restock from bulk (02 to 03)"; ArrowTarget = Bulk.Slots.Count > 0 ? Bulk.Zone.transform.position : Dock.Zone.transform.position; }
                break;
        }
        // a few tutorial steps can't happen yet: say why instead of pointing at an empty station
        if (step == TutStep.Pick && Rack.Stock == 0 && top != Item.Carton) { Hint = "Grab cartons at 02 first"; ArrowTarget = Bulk.Zone.transform.position; }
        if (step == TutStep.TakeParcels && Pack.Cart.Empty) { Hint = Pack.OnInfeed > 0 ? "Keep packing at 05B" : "Pick more orders (04)"; ArrowTarget = Pack.OnInfeed > 0 ? Pack.PackZone.transform.position : Rack.Front.transform.position; }
    }

    // ======================================================================
    void Update()
    {
        float dt = Time.deltaTime;
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown) Sfx.I.StartMusic();
        if (launching) return;
        UpdateOrders(dt);
        UpdateGuidance();
        UpdateQuest();
        if (PowerLeft > 0) PowerLeft = Mathf.Max(0, PowerLeft - dt); else if (PowerCooldown > 0) PowerCooldown -= dt;

        if (AutoPlay && TerminalZone && TerminalZone.PlayerIn && (autoBuyT -= dt) <= 0)
        {
            autoBuyT = 0.4f;
            double c = CheapestUpgrade(out int k);
            if (k >= 0 && Money >= c)
            {
                AddMoney(-c, false);
                if (k == 0) Save.lvSpeed++; else if (k == 1) Save.lvCap++; else if (k == 2) Save.lvProfit++; else Save.lvStaff++;
                Sfx.I.LevelUp();
            }
        }

        AdaptQuality();
        rateT += dt;
        if (rateT >= 1f) { Save.rate = Save.rate * 0.97 + earnedThisSecond / rateT * 0.03; earnedThisSecond = 0; rateT = 0; }
        saveT += dt;
        if (saveT > (dirty ? 3f : 6f)) { saveT = 0; Persist(); }
        if (Dev && Application.absoluteURL.Contains("cheat=1") && Input.GetKeyDown(KeyCode.M)) AddMoney(1000 * DepotMult, true);
    }

    float qT, qFrames; int qLevel;
    void AdaptQuality()
    {
        if (qLevel >= 2) return;
        qT += Time.unscaledDeltaTime; qFrames++;
        if (qT < (qLevel == 0 ? 6f : 4f)) return;
        float fps = qFrames / qT; qT = 0; qFrames = 0;
        if (fps >= 40f) { qLevel = 2; return; }
        qLevel++;
        if (qLevel == 1) { sun.shadows = LightShadows.None; QualitySettings.antiAliasing = 0; }
        WebBridge.Event("quality_drop", (int)fps);
    }

    void LateUpdate()
    {
        if (!Player || launching) return;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        // landscape shows nearly the whole floor; portrait (phones) frames the player's part of it and follows closely
        float t = Mathf.InverseLerp(0.46f, 1.4f, aspect);
        float dist = Mathf.Lerp(32f, 31f, t), follow = Mathf.Lerp(0.95f, 0.35f, t);
        var p = Player.transform.position;
        var focus = new Vector3(Mathf.Clamp(p.x * follow, -Mathf.Lerp(9.5f, 6f, t), Mathf.Lerp(9.5f, 6f, t)), 0,
            Mathf.Clamp(-0.6f + (p.z + 0.6f) * follow, Mathf.Lerp(-1.8f, -5f, t), Mathf.Lerp(5.5f, 3.5f, t)));
        var off = Quaternion.Euler(Sign.Pitch, 0, 0) * Vector3.back * dist;
        var want = focus + off;
        Cam.transform.position = Vector3.Lerp(Cam.transform.position, want, 1f - Mathf.Exp(-Time.deltaTime * 6f));
        Cam.transform.rotation = Quaternion.Euler(Sign.Pitch, 0, 0);
    }

    // dev: SendMessage("Game", "DevLog", "")
    public void DevLog(string _)
    {
        if (!Dev) return;
        Debug.Log("[DEV] pos=" + Player.transform.position.ToString("F1") + " hands=" + Player.Stack.Count + (Player.Stack.Empty ? "" : " " + Player.Stack.TopType) + " tut=" + (TutStep)Save.tut + " " + Player.DebugState + " rack=" + Rack.Stock + " orders=" + Orders + " money=" + (int)Money);
        foreach (var w in Workers) Debug.Log("[DEV]   " + w.Debug);
    }

    // ======================================================================
    // FX
    public void Toast(string msg)
    {
        if (restoring || !Player) return;
        UI.I.FloatText(Player.transform.position + Vector3.up * 2.8f, msg, Color.white, 40);
    }

    public void Burst(Vector3 pos, Color c) => StartCoroutine(BurstRing(pos, c));

    IEnumerator BurstRing(Vector3 pos, Color c)
    {
        var ring = Kit.FloorQuad("burst", Kit.Ring, c, 0.5f, null, pos, 0.05f);
        float k = 0;
        while (k < 1f)
        {
            k += Time.deltaTime / 0.45f;
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 4f, 1f - (1f - k) * (1f - k));
            ring.sharedMaterial.color = Kit.A(c, 1f - k);
            yield return null;
        }
        Destroy(ring.sharedMaterial);
        Destroy(ring.gameObject);
    }

    IEnumerator PopIn(Transform t)
    {
        if (!t) yield break;
        var s = t.localScale;
        float k = 0;
        while (k < 1f && t)
        {
            k += Time.deltaTime / 0.45f;
            t.localScale = s * Kit.EaseOutBack(Mathf.Clamp01(k));
            yield return null;
        }
        if (t) t.localScale = s;
        NavGrid.MarkDirty();
    }
}
