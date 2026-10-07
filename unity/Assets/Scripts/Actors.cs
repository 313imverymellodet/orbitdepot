using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------- tiny tween for things flying into hands/bins
public class Fly : MonoBehaviour
{
    static Fly inst;
    class F { public Transform t, target; public Vector3 off, from; public float k, dur; public Action done; public Vector3 scale; }
    readonly List<F> list = new List<F>();

    public static void To(Transform t, Transform target, Vector3 offset, float dur, Action done)
    {
        if (!inst) inst = new GameObject("Fly").AddComponent<Fly>();
        t.SetParent(null, true);
        inst.list.Add(new F { t = t, target = target, off = offset, from = t.position, dur = dur, done = done, scale = t.localScale });
    }

    void Update()
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var f = list[i];
            f.k += Time.deltaTime / f.dur;
            float k = Mathf.Clamp01(f.k), e = k * k;
            var to = f.target ? f.target.position + f.off : f.from;
            f.t.position = Vector3.Lerp(f.from, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
            f.t.localScale = f.scale * Mathf.Lerp(1f, 0.6f, e);
            if (f.k >= 1f) { list.RemoveAt(i); f.t.localScale = f.scale; f.done?.Invoke(); }
        }
    }
}

// ---------------------------------------------------------------- shared character rig helpers
public static class Rig
{
    public static Animation Setup(GameObject root)
    {
        var anim = root.GetComponentInChildren<Animation>();
        if (!anim) return null;
        anim.cullingType = AnimationCullingType.AlwaysAnimate;
        foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
        var hold = anim["holding-both"];
        if (hold != null)
        {
            hold.layer = 1;
            hold.weight = 0;
            foreach (var t in anim.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("arm-")) hold.AddMixingTransform(t, true);
            hold.enabled = true;
        }
        anim.Play("idle");
        return anim;
    }

    public static void Loco(Animation anim, float speed01, bool carrying, float walkRate = 1.4f)
    {
        if (!anim) return;
        string clip = speed01 > 0.08f ? "walk" : "idle";
        if (!anim.IsPlaying(clip)) anim.CrossFade(clip, 0.15f);
        if (clip == "walk") anim["walk"].speed = Mathf.Max(0.6f, speed01) * walkRate;
        var hold = anim["holding-both"];
        if (hold != null)
        {
            hold.enabled = true;
            hold.weight = Mathf.MoveTowards(hold.weight, carrying ? 1f : 0f, Time.deltaTime * 8f);
        }
    }

    static Material hatMat, vestMat;
    // Warehouse kit: a hard hat (and a hi-vis vest stripe) on a Kenney mini character.
    public static void Dress(GameObject model, Color hat, Color vest)
    {
        Transform head = null;
        foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "head") { head = t; break; }
        var b = Kit.WorldBounds(model);
        var hatRoot = new GameObject("hardhat").transform;
        if (head) { hatRoot.SetParent(head, true); hatRoot.position = new Vector3(head.position.x, b.max.y - 0.08f, head.position.z); }
        else { hatRoot.SetParent(model.transform, true); hatRoot.position = model.transform.position + Vector3.up * (b.size.y - 0.08f); }
        hatRoot.rotation = model.transform.rotation;
        var m = new Material(Shader.Find("Standard")) { color = hat }; m.SetFloat("_Glossiness", 0.55f);
        float inv = 1f / Mathf.Max(0.01f, hatRoot.lossyScale.x);
        var dome = Kit.MeshObj("dome", Kit.SphereMesh, m, hatRoot);
        dome.transform.localScale = new Vector3(0.5f, 0.32f, 0.52f) * inv;
        var brim = Kit.MeshObj("brim", Kit.SphereMesh, m, hatRoot);
        brim.transform.localScale = new Vector3(0.62f, 0.035f, 0.66f) * inv;
        brim.transform.localPosition = new Vector3(0, -0.03f, 0.04f) * inv;
    }
}

// ---------------------------------------------------------------- Player
public class Player : MonoBehaviour
{
    public ItemStack Stack;
    CharacterController cc;
    Animation anim;
    float nextTick, nextCash, yaw;
    Vector3 vel;
    WorldTag maxTag;

    public static Player Create(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Player");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;
        var p = root.AddComponent<Player>();
        var m = Kit.Spawn("Characters/character-male-e", Kit.CharScale, root.transform);
        p.anim = Rig.Setup(m);
        Rig.Dress(m, Kit.Hex("#ffc93c"), Kit.Hex("#ff8a3c"));
        root.layer = NavGrid.IgnoreLayer;
        p.cc = root.AddComponent<CharacterController>();
        p.cc.radius = 0.32f; p.cc.height = 1.2f; p.cc.center = new Vector3(0, 0.62f, 0); p.cc.stepOffset = 0.2f; p.cc.skinWidth = 0.03f;
        var s = new GameObject("Stack");
        s.transform.SetParent(root.transform, false);
        s.transform.localPosition = new Vector3(0, 0.62f, 0.42f);
        p.Stack = s.AddComponent<ItemStack>();
        p.Stack.FlyTime = 0.22f; p.Stack.ArcHeight = 0.8f;
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.3f), 0.9f, root.transform, Vector3.zero, 0.015f);
        Kit.FloorQuad("you", Kit.Ring, Kit.A(Kit.Hex("#ffe066"), 0.9f), 1.1f, root.transform, Vector3.zero, 0.02f);
        var you = UI.I.Tag(root.transform, Vector3.up * 2.05f, false);
        you.Set(null, "YOU"); you.Small = true;
        p.maxTag = UI.I.Tag(root.transform, Vector3.up * 2.7f, false);
        p.maxTag.Set("MAX", null);
        return p;
    }

    public bool Tick()
    {
        if (Time.time < nextTick) return false;
        nextTick = Time.time + 0.09f;
        return true;
    }

    public bool CashTick()
    {
        if (Time.time < nextCash) return false;
        nextCash = Time.time + 0.035f;
        return true;
    }

    public Vector3 Velocity => vel;
    public void Teleport(Vector3 pos) { cc.enabled = false; transform.position = pos; cc.enabled = true; }

    // ---- ?bot=1 autoplay: walks the whole fulfilment loop. Used for testing and trailer capture.
    Vector3? autoTarget; float autoT;
    List<Vector3> autoPath = new List<Vector3>();
    Zone autoZone;
    public string DebugState => (autoZone ? autoZone.name : "-") + " path=" + string.Join(" ", autoPath.ConvertAll(v => v.ToString("F1"))) + " target=" + autoTarget + " vel=" + vel.magnitude.ToString("F2") + " grounded=" + (cc ? cc.isGrounded : false);

    Vector2 AutoInput()
    {
        var g = Game.I;
        autoT -= Time.deltaTime;
        var pos = transform.position;
        if (autoT <= 0 || autoTarget == null)
        {
            autoT = 0.35f;
            var z = ChooseZone(g);
            var t = z ? z.transform.position : (Vector3?)null;
            if (z && z.Has(pos)) { autoTarget = t; autoPath.Clear(); }
            else if (t.HasValue && (autoTarget == null || (t.Value - autoTarget.Value).sqrMagnitude > 0.01f || autoPath.Count == 0))
                autoPath = NavGrid.Path(pos, t.Value);
            autoTarget = t; autoZone = z;
        }
        if (autoTarget == null || (autoZone && autoZone.Has(pos) && (autoZone.transform.position - pos).magnitude < 0.35f)) return Vector2.zero;
        if (autoPath.Count == 0) autoPath.Add(autoTarget.Value);
        var to = autoPath[0];
        var d = to - pos; d.y = 0;
        if (d.magnitude < 0.3f)
        {
            if (autoPath.Count > 1) { autoPath.RemoveAt(0); to = autoPath[0]; d = to - pos; d.y = 0; }
            else
            {
                var rest = autoTarget.Value - pos; rest.y = 0;
                if (rest.magnitude > 0.3f) d = rest; else return Vector2.zero;
            }
        }
        var n = d.normalized * Mathf.Clamp01(d.magnitude * 2f + 0.3f);
        return new Vector2(n.x, n.z);
    }

    // Stay put while the current zone still has work for us; otherwise pick the next job in the flow.
    Zone ChooseZone(Game g)
    {
        var pos = transform.position;
        Item? top = Stack.Empty ? (Item?)null : Stack.TopType;
        bool full = Stack.Full || (top.HasValue && Items.Bulky(top.Value));
        if (autoZone && autoZone.Has(pos))
        {
            if (autoZone == g.Rack.Front && !full && g.Orders > 0 && g.Rack.Stock > 0 && (top == null || top == Item.Tote)) return autoZone;
            if (autoZone == g.Pack.PackZone && g.Pack.OnInfeed > 0 && !g.Pack.PackerBot && g.Pack.Cart.Count < g.Pack.Cart.Capacity) return autoZone;
            if (autoZone == g.Bulk.Zone && !full && g.Bulk.Slots.Count > 0 && (top == null || top == Item.Carton) && g.Rack.Stock + Stack.Count < g.Rack.Cap) return autoZone;
            if (autoZone == g.Out.Cash.Zone && !g.Out.Cash.Pile.Empty) return autoZone;
        }
        if (top == Item.Gaylord) return g.Out.Zone;
        if (top == Item.Parcel) return g.Sort.DropZone;
        if (top == Item.Tote) return g.Pack.DropZone;
        if (top == Item.Carton) return g.Rack.Back;
        if (top == Item.Pallet) return g.Bulk.Zone;
        if (g.Out.Cash.Pile.Count >= 4) return g.Out.Cash.Zone;
        var pad = g.AffordablePad();
        if (pad) return pad.Zone;
        if (g.AutoPlay && g.TerminalZone && g.Money >= g.CheapestUpgrade(out _) * 1.3) return g.TerminalZone;
        if (g.Pack.OnInfeed > 0 && !g.Pack.PackerBot && g.Pack.Cart.Count < g.Pack.Cart.Capacity) return g.Pack.PackZone;
        if (!g.HasJob(Worker.Job.Forklift) && g.Sort.InBins >= 8) return g.Sort.LiftZone;
        if (!g.HasJob(Worker.Job.Sorter) && g.Pack.Cart.Count >= 4) return g.Pack.CartZone;
        if (!g.HasJob(Worker.Job.Picker) && g.Orders > 0 && g.Rack.Stock > 0) return g.Rack.Front;
        if (!g.HasJob(Worker.Job.Restocker) && g.Bulk.Slots.Count > 0 && g.Rack.Stock < g.Rack.Cap - 4) return g.Bulk.Zone;
        if (!g.HasJob(Worker.Job.Pally) && g.Dock.Ready.Count > 0 && !g.Bulk.Slots.Full) return g.Dock.Zone;
        if (g.Sort.InBins > 0 && !g.HasJob(Worker.Job.Forklift)) return g.Sort.LiftZone;
        if (g.Pack.Cart.Count > 0 && !g.HasJob(Worker.Job.Sorter)) return g.Pack.CartZone;
        if (!g.Out.Cash.Pile.Empty) return g.Out.Cash.Zone;
        return g.Pack.PackZone;
    }

    void Update()
    {
        Stack.Capacity = Game.I.Capacity;
        var input = UI.I.Joy;
        float kx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        float ky = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        if (kx != 0 || ky != 0) input = new Vector2(kx, ky).normalized;
        if (Game.I.AutoPlay) input = AutoInput();
        if (UI.I.Blocking) input = Vector2.zero;

        var dir = new Vector3(input.x, 0, input.y);
        float mag = Mathf.Clamp01(dir.magnitude);
        bool heavy = !Stack.Empty && Items.Bulky(Stack.TopType);
        var target = dir.normalized * Game.I.MoveSpeed * mag * (heavy ? 0.82f : 1f);
        vel = Vector3.Lerp(vel, target, 1f - Mathf.Exp(-Time.deltaTime * 14f));
        cc.SimpleMove(vel);
        if (mag > 0.05f)
        {
            float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, want, Time.deltaTime * 900f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Game.I.Tutorial(TutStep.Move);
        }
        Rig.Loco(anim, vel.magnitude / Game.I.MoveSpeed, !Stack.Empty);
        // bulky loads (pallets, bins) ride low on a hand truck; small stuff makes a wobbly tower
        Stack.transform.localPosition = heavy ? new Vector3(0, 0.05f, 0.85f) : new Vector3(0, 0.62f, 0.42f);
        var lv = transform.InverseTransformDirection(vel);
        Stack.Sway = heavy ? Vector3.zero : Vector3.Lerp(Stack.Sway, new Vector3(-lv.x, 0, -lv.z) * 0.035f, 1f - Mathf.Exp(-Time.deltaTime * 6f));
        maxTag.Visible = Stack.Full && !heavy;
    }
}

// ---------------------------------------------------------------- Crew
// Hired staff run one leg of the flow on their own: walk to a source zone, load up, walk to a target zone, unload.
public class Worker : MonoBehaviour
{
    public enum Job { Pally, Restocker, Picker, Packer, Sorter, Forklift, DockCrew }
    public Job Kind;
    public ItemStack Stack;
    public string Name;
    readonly List<Vector3> path = new List<Vector3>();
    Animation anim;
    Transform body;
    Vector3 home;
    float yaw, workT, idleT, speed = 2.6f, stuckT;
    Vector3 lastPos;
    int state;
    bool vehicle;
    WorldTag tag;

    public static readonly string[] Names = { "PALLY", "RIO", "JUNE", "KAI", "NOVA", "FORK", "ELI" };
    public static string Title(Job j) => j switch
    {
        Job.Pally => "PALLET JACK", Job.Restocker => "RESTOCK", Job.Picker => "PICKER", Job.Packer => "PACKER",
        Job.Sorter => "SORTER", Job.Forklift => "FORKLIFT", _ => "DOCK CREW",
    };

    public static Worker Create(Transform parent, Job job, Vector3 home)
    {
        var root = new GameObject("Worker_" + job);
        root.transform.SetParent(parent, false);
        root.transform.position = home;
        var w = root.AddComponent<Worker>();
        w.Kind = job; w.home = home; w.Name = Names[(int)job];
        w.vehicle = job == Job.Pally || job == Job.Forklift;
        if (w.vehicle)
        {
            // a little space truck does the heavy lifting
            var m = Kit.Spawn(job == Job.Pally ? "Base/spacetruck" : "Base/spacetruck_large", 1f, root.transform);
            var b = Kit.WorldBounds(m);
            m.transform.localScale = Vector3.one * (1.25f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)));
            w.body = m.transform;
            w.speed = 3.0f;
        }
        else
        {
            string[] looks = { "male-a", "female-b", "male-c", "female-d", "male-f", "female-a", "male-b" };
            var m = Kit.Spawn("Characters/character-" + looks[(int)job % looks.Length], Kit.CharScale, root.transform);
            w.anim = Rig.Setup(m);
            Rig.Dress(m, Kit.Hex("#ffc93c"), Kit.Hex("#9dff6b"));
            w.body = m.transform;
        }
        var s = new GameObject("Stack");
        s.transform.SetParent(root.transform, false);
        s.transform.localPosition = w.vehicle ? new Vector3(0, 0.45f, -0.25f) : new Vector3(0, 0.62f, 0.42f);
        w.Stack = s.AddComponent<ItemStack>();
        w.Stack.Capacity = 4; w.Stack.FlyTime = 0.22f; w.Stack.ArcHeight = 0.7f;
        if (!w.vehicle) { w.Stack.Cols = 2; w.Stack.Rows = 2; w.Stack.Cell = new Vector2(0.4f, 0.36f); }   // crew carry a neat block, not a tower
        Kit.FloorQuad("shadow", Kit.Disc, new Color(0, 0, 0, 0.25f), 0.9f, root.transform, Vector3.zero, 0.015f);
        Kit.FloorQuad("jobring", Kit.Ring, Kit.A(Kit.Hex("#9dff6b"), 0.85f), 1.05f, root.transform, Vector3.zero, 0.02f);
        if (job != Job.DockCrew)   // ELI's name is on the dock's BUILDING sign
        {
            w.tag = UI.I.Tag(root.transform, Vector3.up * (w.vehicle ? 1.6f : 2.05f), false);
            w.tag.Set(null, w.Name + " · " + Title(job)); w.tag.Small = true;
        }
        if (job == Job.Packer) Game.I.Pack.PackerBot = true;
        return w;
    }

    public string Debug => Name + " s" + state + " n" + Stack.Count + " p" + transform.position.ToString("F1") + " path" + path.Count;
    bool Arrived => path.Count == 0;
    void GoTo(Vector3 to) { path.Clear(); path.AddRange(NavGrid.Path(transform.position, to)); if (path.Count == 0) path.Add(to); }
    bool Tick(float rate) { workT += Time.deltaTime; if (workT < rate / Game.I.StaffSpeed) return false; workT = 0; return true; }
    static Vector3 Spot(Zone z, float off) => z.transform.position + new Vector3(off, 0, -0.35f);

    void Update()
    {
        float dt = Time.deltaTime;
        // walking but not getting anywhere (a long hitch, a rebuilt nav grid): plan the route again
        if (path.Count > 0 && (transform.position - lastPos).sqrMagnitude < 0.0004f) { if ((stuckT += dt) > 2f) { stuckT = 0; var goal = path[path.Count - 1]; GoTo(goal); } }
        else stuckT = 0;
        lastPos = transform.position;
        if (path.Count > 0)
        {
            var d = path[0] - transform.position; d.y = 0;
            float step = speed * Game.I.StaffSpeed * dt;
            if (d.magnitude <= step) { transform.position = new Vector3(path[0].x, 0, path[0].z); path.RemoveAt(0); }
            else
            {
                transform.position += d.normalized * step;
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 600f);
            }
        }
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        Stack.Capacity = vehicle ? 1 : 6 + 2 * Game.I.Save.lvStaff;
        if (anim) Rig.Loco(anim, Arrived ? 0 : 1, !Stack.Empty);
        if (vehicle && body) body.localPosition = new Vector3(0, Arrived ? 0 : Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.03f, 0);

        var g = Game.I;
        switch (Kind)
        {
            case Job.Pally: Shuttle(g.Dock.Zone, () => g.Dock.TakePallet(Stack), g.Bulk.Zone, () => g.Bulk.Store(Stack), () => g.Dock.Ready.Count > 0 && !g.Bulk.Slots.Full, 0.3f); break;
            case Job.Restocker: Shuttle(g.Bulk.Zone, () => g.Rack.Stock + Stack.Count < g.Rack.Cap && g.Bulk.TakeCarton(Stack), g.Rack.Back, () => g.Rack.Restock(Stack), () => g.Bulk.Slots.Count > 0 && g.Rack.Stock < g.Rack.Cap - 2, 0.15f); break;
            case Job.Picker: Shuttle(g.Rack.Front, () => g.Rack.Pick(Stack, null, 0.15f, 1f), g.Pack.DropZone, () => g.Pack.Drop(Stack), () => g.Orders > 0 && g.Rack.Stock > 0 && g.Pack.OnInfeed < g.Pack.InfeedCap, 0.15f); break;
            case Job.Sorter: Shuttle(g.Pack.CartZone, () => g.Pack.TakeParcels(Stack), g.Sort.DropZone, () => g.Sort.Drop(Stack), () => g.Pack.Cart.Count >= 3, 0.12f); break;
            case Job.Forklift: Shuttle(g.Sort.LiftZone, () => g.Sort.TakeBin(Stack), g.Out.Zone, () => g.Out.Ship(Stack), () => g.Sort.InBins >= 10 || (g.Sort.InBins > 0 && g.Sort.Chute.Empty && g.Pack.Cart.Empty && idleT > 6f), 0.4f); break;
            case Job.Packer: case Job.DockCrew: StandWork(); break;
        }
    }

    void StandWork()
    {
        if (!Arrived) return;
        yaw = Mathf.MoveTowardsAngle(yaw, 0, Time.deltaTime * 300f);
        if (anim && Kind == Job.Packer && Game.I.Pack.Packing) body.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 10f) * 12f, 0);
    }

    // 0 wait for work, 1 walking to source, 2 loading, 3 walking to target, 4 unloading
    void Shuttle(Zone src, Func<bool> take, Zone dst, Func<bool> give, Func<bool> worth, float rate)
    {
        float dt = Time.deltaTime;
        switch (state)
        {
            case 0:
                idleT += dt;
                if (!Stack.Empty) { GoTo(Spot(dst, 0.45f)); state = 3; break; }
                if (worth()) { GoTo(Spot(src, 0.45f)); state = 1; idleT = 0; }
                else if (Arrived && (transform.position - home).sqrMagnitude > 0.2f) GoTo(home);
                break;
            case 1: if (Arrived) { state = 2; idleT = 0; } break;
            case 2:
                idleT += dt;
                if (Tick(rate) && take()) idleT = 0;
                bool bulky = !Stack.Empty && Items.Bulky(Stack.TopType);
                if (Stack.Full || bulky || (idleT > 1.0f && !Stack.Empty) || (idleT > 2.5f && Stack.Empty))
                {
                    idleT = 0;
                    if (Stack.Empty) state = 0;
                    else { GoTo(Spot(dst, 0.45f)); state = 3; }
                }
                break;
            case 3: if (Arrived) { state = 4; idleT = 0; } break;
            case 4:
                idleT += dt;
                if (Tick(rate) && give()) idleT = 0;
                if (Stack.Empty || idleT > 3f) { state = 0; idleT = 0; }
                break;
        }
    }

    void OnDestroy() { tag?.Destroy(); }
}
