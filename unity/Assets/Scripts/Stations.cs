using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ======================================================================
// Floor + signage
// ======================================================================

// A glowing floor circle. Everything interactive in the depot is "stand here".
public class Zone : MonoBehaviour
{
    public float Radius = 0.9f;
    public Color Color = Color.white;
    MeshRenderer ring, fill;
    public float Progress = -1f;   // >=0 shows a fill disc (pads)
    float pulse;

    public static Zone Make(Transform parent, Vector3 pos, float radius, Color c, string name = "zone", string number = null, string caption = "WALK HERE")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var z = go.AddComponent<Zone>();
        z.Radius = radius; z.Color = c;
        z.ring = Kit.FloorQuad("ring", Kit.Ring, c, radius * 2f, go.transform, Vector3.zero, 0.03f);
        z.fill = Kit.FloorQuad("fill", Kit.Disc, Kit.A(c, 0.45f), 0.01f, go.transform, Vector3.zero, 0.035f);
        if (number != null)
        {
            FloorText.Make(go.transform, new Vector3(0, 0, 0.12f), number, 64, Kit.A(c, 0.55f), 0);
            if (!string.IsNullOrEmpty(caption)) FloorText.Make(go.transform, new Vector3(0, 0, -0.38f), caption, 24, Kit.A(c, 0.5f), 0);
        }
        return z;
    }

    public bool Has(Vector3 p)
    {
        var d = p - transform.position; d.y = 0;
        return d.sqrMagnitude < Radius * Radius;
    }

    public bool PlayerIn => Game.I.Player && Has(Game.I.Player.transform.position);

    void Update()
    {
        bool inside = PlayerIn;
        pulse = Mathf.MoveTowards(pulse, inside ? 1 : 0, Time.deltaTime * 6f);
        float s = Radius * 2f * (1f + pulse * 0.08f + Mathf.Sin(Time.time * 4f) * 0.015f);
        ring.transform.localScale = Vector3.one * s;
        ring.sharedMaterial.color = Kit.A(Color, 0.55f + pulse * 0.45f);
        if (Progress >= 0)
        {
            fill.enabled = true;
            fill.transform.localScale = Vector3.one * Radius * 1.75f * Mathf.Clamp01(Progress);
        }
        else
        {
            // standing in a zone lights it up
            fill.enabled = pulse > 0.01f;
            fill.transform.localScale = Vector3.one * Radius * 1.8f * pulse;
        }
    }
}

// Text painted on the floor (lane names, the depot logo, zone numbers).
public static class FloorText
{
    public static Text Make(Transform parent, Vector3 pos, string s, int size, Color c, float yaw, float width = 1200, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("floortext", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = -5;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, size * 1.5f);
        rt.localPosition = pos + Vector3.up * 0.025f;
        rt.localRotation = Quaternion.Euler(90, yaw, 0);
        rt.localScale = Vector3.one * 0.01f;
        var t = new GameObject("t", typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(go.transform, false);
        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        t.font = Kit.Font; t.fontSize = size; t.alignment = align; t.color = c; t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        return t;
    }
}

// An in-world signboard: dark card, accent stripe, small coloured title, big white value.
// Styles: Card (station status), Header (dock door), Callout (task nudges), Label (small tag).
public class Sign : MonoBehaviour
{
    public enum Style { Card, Header, Callout, Label }
    public Text Title, Value;
    public Image Bg, Stripe, Dot, BarBg, BarFill;
    float barShown = -1f;
    public Color Accent;
    public float BobAmp;
    RectTransform rt;
    Vector3 basePos;
    float punch;
    public const float Pitch = 50f;
    static readonly Color Ink = Kit.Hex("#0c1322");

    public static Sign Make(Transform parent, Vector3 pos, float width, Color accent, string title, Style style = Style.Card)
    {
        float h = style == Style.Card ? 1.32f : style == Style.Header ? 1.35f : style == Style.Callout ? 0.62f : 0.42f;
        var go = new GameObject("sign", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = style == Style.Callout ? 12 : 10;
        var s = go.AddComponent<Sign>();
        s.Accent = accent;
        s.rt = (RectTransform)go.transform;
        s.rt.sizeDelta = new Vector2(width * 100, h * 100);
        s.rt.localPosition = pos; s.basePos = pos;
        s.rt.localRotation = Quaternion.Euler(Pitch, 0, 0);
        s.rt.localScale = Vector3.one * 0.01f;

        // drop shadow + card
        var sh = Img(s.rt, Kit.RoundedSprite, new Color(0, 0, 0, 0.35f)); Stretch(sh.rectTransform, -2); sh.rectTransform.anchoredPosition = new Vector2(5, -7);
        s.Bg = Img(s.rt, Kit.RoundedSprite, Kit.A(style == Style.Callout ? Kit.Hex("#0a0f1c") : Ink, 0.93f)); Stretch(s.Bg.rectTransform, 0);
        if (style == Style.Card || style == Style.Label)
        {
            s.Stripe = Img(s.rt, null, accent);
            var srt = s.Stripe.rectTransform; srt.anchorMin = new Vector2(0, 0.12f); srt.anchorMax = new Vector2(0, 0.88f); srt.sizeDelta = new Vector2(style == Style.Label ? 6 : 9, 0); srt.anchoredPosition = new Vector2(style == Style.Label ? 10 : 14, 0);
        }
        if (style == Style.Header)
        {
            // hazard-striped top edge, like a dock door frame
            var top = Img(s.rt, null, Kit.A(accent, 0.95f)); var trt = top.rectTransform;
            trt.anchorMin = new Vector2(0.03f, 1); trt.anchorMax = new Vector2(0.97f, 1); trt.sizeDelta = new Vector2(0, 8); trt.anchoredPosition = new Vector2(0, -6);
        }
        if (style == Style.Card || style == Style.Label)
        {
            // thin progress bar along the bottom edge (hidden until a station feeds it)
            s.BarBg = Img(s.rt, null, new Color(1, 1, 1, 0.13f)); var brt = s.BarBg.rectTransform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.offsetMin = new Vector2(style == Style.Label ? 22 : 34, style == Style.Label ? 4 : 9); brt.offsetMax = new Vector2(-22, (style == Style.Label ? 4 : 9) + (style == Style.Label ? 4 : 6));
            s.BarFill = Img(brt, null, accent); var frt = s.BarFill.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0, 1); frt.offsetMin = frt.offsetMax = Vector2.zero;
            s.BarBg.gameObject.SetActive(false);
        }
        switch (style)
        {
            case Style.Card:
                s.Title = Txt(s.rt, title, 30, accent, TextAnchor.UpperLeft, new Vector2(34, -12));
                s.Value = Txt(s.rt, "", 50, Color.white, TextAnchor.LowerLeft, new Vector2(34, 17));
                s.Dot = Img(s.rt, Kit.DiscSprite, accent); var drt = s.Dot.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(1, 1); drt.sizeDelta = new Vector2(14, 14); drt.anchoredPosition = new Vector2(-22, -24);
                s.Dot.gameObject.SetActive(false);
                break;
            case Style.Header:
                s.Title = Txt(s.rt, title, 32, Kit.A(accent, 0.95f), TextAnchor.UpperCenter, new Vector2(0, -20));
                s.Value = Txt(s.rt, "", 54, Color.white, TextAnchor.LowerCenter, new Vector2(0, 14));
                break;
            case Style.Callout:
                s.Value = Txt(s.rt, title, 40, Color.white, TextAnchor.MiddleCenter, Vector2.zero);
                s.BobAmp = 0.06f;
                break;
            case Style.Label:
                s.Value = Txt(s.rt, title, 26, accent, TextAnchor.MiddleLeft, new Vector2(26, 0));
                break;
        }
        return s;
    }

    static void Stretch(RectTransform r, float inset) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); }

    static Image Img(Transform p, Sprite sp, Color c)
    {
        var go = new GameObject("img", typeof(RectTransform)); go.transform.SetParent(p, false);
        var i = go.AddComponent<Image>(); i.sprite = sp; i.color = c; i.raycastTarget = false;
        if (sp == Kit.RoundedSprite) i.type = Image.Type.Sliced;
        return i;
    }

    static Text Txt(Transform p, string s, int size, Color c, TextAnchor a, Vector2 off)
    {
        var go = new GameObject("txt", typeof(RectTransform)); go.transform.SetParent(p, false);
        var t = go.AddComponent<Text>();
        var r = t.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(Mathf.Max(0, off.x) + 4, Mathf.Max(0, off.y) + 2); r.offsetMax = new Vector2(-Mathf.Max(0, off.x) - 4, Mathf.Min(0, off.y) - 2);
        t.font = Kit.Font; t.fontSize = size; t.color = c; t.alignment = a; t.text = s; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    string lastValue;
    public void Set(string value, bool flashDot = false)
    {
        if (Value && value != lastValue) { if (lastValue != null) punch = 1f; Value.text = value; lastValue = value; }
        if (Dot) Dot.gameObject.SetActive(flashDot);
    }

    // 0..1 fill along the bottom; eases toward the target so counters feel alive
    public void Bar(float v)
    {
        if (!BarBg) return;
        if (!BarBg.gameObject.activeSelf) BarBg.gameObject.SetActive(true);
        v = Mathf.Clamp01(v);
        barShown = barShown < 0 ? v : Mathf.Lerp(barShown, v, 1f - Mathf.Exp(-Time.deltaTime * 8f));
        BarFill.rectTransform.anchorMax = new Vector2(barShown, 1);
        BarFill.color = barShown > 0.98f ? Color.Lerp(Accent, Color.white, Mathf.PingPong(Time.time * 2f, 1f) * 0.5f) : Accent;
    }

    public bool Visible { get => gameObject.activeSelf; set { if (gameObject.activeSelf != value) gameObject.SetActive(value); } }

    void LateUpdate()
    {
        punch = Mathf.MoveTowards(punch, 0, Time.deltaTime * 4f);
        rt.localScale = Vector3.one * 0.01f * (1f + punch * 0.06f);
        if (BobAmp > 0) rt.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 3f) * BobAmp;
        if (Dot && Dot.gameObject.activeSelf) Dot.color = Kit.A(Accent, 0.4f + 0.6f * Mathf.PingPong(Time.time * 2f, 1f));
    }
}

// ======================================================================
// 01 Inbound dock: space trucks drop loose cartons, the dock crew builds pallets of 8.
// ======================================================================
public class InboundDock : MonoBehaviour
{
    public ItemStack Loose, Ready;
    public Transform BuildSpot, Truck, Crew;
    public Zone Zone;
    public Sign Header, BuildSign, ReadySign;
    public Vector3 TruckDock, TruckAway;
    public int LooseCap = 12;
    Transform building; Payload buildLoad; int buildTaken, buildLanded;
    float buildT, truckT = 1.5f, unloadT;
    int truckState, toUnload;     // 0 away, 1 arriving, 2 unloading, 3 leaving
    float truckK;

    public float BuildInterval => 0.75f / Game.I.CrewSpeed;
    public int ReadyBoxes { get { int n = 0; for (int i = 0; i < Ready.Count; i++) n += Ready.At(i).GetComponent<Payload>().Count; return n; } }

    void Update()
    {
        float dt = Time.deltaTime;
        TruckCycle(dt);

        // the crew palletises loose cartons
        if (!building && Ready.Count < Ready.Capacity && !Loose.Empty)
        {
            building = ItemPool.Get(Item.Pallet);
            building.position = BuildSpot.position; building.rotation = BuildSpot.rotation;
            buildLoad = building.GetComponent<Payload>(); buildLoad.Set(0);
            buildTaken = buildLanded = 0;
        }
        if (building && !Loose.Empty && buildTaken < 8 && (buildT += dt) >= BuildInterval)
        {
            buildT = 0;
            var t = Loose.Pop(out _);
            var load = buildLoad;
            int n = buildTaken++;
            // the carton flies onto the pallet; its slot fills in when it lands
            Fly.To(t, building, new Vector3((n % 4 % 2 - 0.5f) * 0.48f, 0.14f + (n / 4) * 0.4f, (n % 4 / 2 - 0.5f) * 0.48f), 0.22f, () =>
            {
                ItemPool.Release(Item.Carton, t);
                if (load == buildLoad) { buildLanded++; load.Set(buildLanded); }
            });
        }
        if (building && buildLanded >= 8 && (buildT += dt) > 0.35f)
        {
            buildT = 0;
            buildLoad.Set(8);
            Ready.Push(Item.Pallet, building);
            building = null; buildLoad = null;
            Sfx.I.Drop();
        }

        // player picks up a pallet
        var p = Game.I.Player;
        if (Zone.PlayerIn && TakePallet(p.Stack, p)) Game.I.Tutorial(TutStep.TakePallet);

        int boxes = ReadyBoxes;
        Header.Set(Loose.Count + " LOOSE CARTON" + (Loose.Count == 1 ? "" : "S"));
        BuildSign.Set(building ? "BUILDING " + buildLanded + " / 8" : Ready.Count >= Ready.Capacity ? "PALLETS FULL" : "WAITING FOR TRUCK");
        BuildSign.Bar(building ? buildLanded / 8f : 0f);
        ReadySign.Set(Ready.Count + " PALLET" + (Ready.Count == 1 ? "" : "S") + " · " + boxes + " BOXES", Ready.Count > 0);
        ReadySign.Bar(Ready.Count / (float)Ready.Capacity);
    }

    public bool TakePallet(ItemStack into, Player who = null)
    {
        if (Ready.Empty || !into.CanAccept(Item.Pallet) || (who && !who.Tick())) return false;
        Ready.TransferTo(into);
        Sfx.I.Pop();
        return true;
    }

    void TruckCycle(float dt)
    {
        if (!Truck) return;
        switch (truckState)
        {
            case 0:
                if ((truckT -= dt) <= 0 && Loose.Count <= LooseCap - 6) { truckState = 1; truckK = 0; Truck.gameObject.SetActive(true); Sfx.I.Whoosh(); }
                break;
            case 1:
                truckK += dt / 1.6f;
                Truck.position = Vector3.Lerp(TruckAway, TruckDock, 1f - Mathf.Pow(1f - Mathf.Clamp01(truckK), 3));
                if (truckK >= 1f) { truckState = 2; toUnload = UnityEngine.Random.Range(6, 9) + Game.I.TruckBonus; unloadT = 0.2f; }
                break;
            case 2:
                Truck.position = TruckDock + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.03f;
                if ((unloadT -= dt) <= 0)
                {
                    unloadT = 0.16f;
                    if (toUnload > 0 && Loose.Count < LooseCap) { Loose.Add(Item.Carton, Truck.position + Vector3.up * 1.4f); toUnload--; Sfx.I.Pop(); }
                    else { truckState = 3; truckK = 0; Sfx.I.Whoosh(); }
                }
                break;
            case 3:
                truckK += dt / 1.4f;
                Truck.position = Vector3.Lerp(TruckDock, TruckAway, Mathf.Pow(Mathf.Clamp01(truckK), 2));
                if (truckK >= 1f) { truckState = 0; truckT = Game.I.TruckInterval; Truck.gameObject.SetActive(false); }
                break;
        }
    }
}

// ======================================================================
// 02 Bulk storage: pallets wait here; break them down into cartons for the rack.
// ======================================================================
public class BulkStorage : MonoBehaviour
{
    public ItemStack Slots;
    public Zone Zone;
    public Sign Card;

    public int Boxes { get { int n = 0; for (int i = 0; i < Slots.Count; i++) n += Slots.At(i).GetComponent<Payload>().Count; return n; } }

    void Update()
    {
        var p = Game.I.Player;
        if (Zone.PlayerIn)
        {
            if (!p.Stack.Empty && p.Stack.TopType == Item.Pallet) { if (Store(p.Stack, p)) Game.I.Tutorial(TutStep.StorePallet); }
            else if (TakeCarton(p.Stack, p)) Game.I.Tutorial(TutStep.GrabCartons);
        }
        Card.Set(Slots.Count + " PALLET" + (Slots.Count == 1 ? "" : "S") + " · " + Boxes + " BOXES", Slots.Count > 0);
        Card.Bar(Boxes / (float)(Slots.Capacity * 8));
    }

    public bool Store(ItemStack from, Player who = null)
    {
        if (from.Empty || from.TopType != Item.Pallet || Slots.Full || (who && !who.Tick())) return false;
        from.TransferTo(Slots);
        Sfx.I.Drop();
        return true;
    }

    public bool TakeCarton(ItemStack into, Player who = null)
    {
        if (Slots.Empty || !into.CanAccept(Item.Carton) || (who && !who.Tick())) return false;
        var top = Slots.Top; var load = top.GetComponent<Payload>();
        into.Add(Item.Carton, top.position + Vector3.up * (0.2f + load.Count * 0.08f));
        load.Set(load.Count - 1);
        if (load.Count <= 0)
        {
            var t = Slots.Pop(out _);
            load.Set(8);
            ItemPool.Release(Item.Pallet, t);
        }
        Sfx.I.Pop();
        return true;
    }
}

// ======================================================================
// 03 / 04 Pick rack: restock from the back, pick orders from the front.
// ======================================================================
public class PickRack : MonoBehaviour
{
    public Zone Back, Front;
    public Sign BackCard, FrontCard;
    public readonly List<GameObject> SlotBoxes = new List<GameObject>();
    public int Stock, Cap = 32;
    float pickT;

    void Update()
    {
        var p = Game.I.Player;
        if (Back.PlayerIn && Restock(p.Stack, p)) Game.I.Tutorial(TutStep.Restock);
        if (Front.PlayerIn && Pick(p.Stack, p, Time.deltaTime)) Game.I.Tutorial(TutStep.Pick);
        for (int i = 0; i < SlotBoxes.Count; i++) { bool on = i < Stock; if (SlotBoxes[i].activeSelf != on) SlotBoxes[i].SetActive(on); }
        BackCard.Set(Stock + " / " + Cap + " IN THIS RACK", Stock < Cap / 3);
        BackCard.Bar(Stock / (float)Cap);
        FrontCard.Set(Stock + " STOCK · " + Game.I.Orders + " ORDERS", Game.I.Orders > 0 && Stock > 0);
        FrontCard.Bar(Game.I.Orders / 40f);
        Front.Progress = Front.PlayerIn && pickT > 0 ? pickT / Game.I.PickTime : -1f;
    }

    public bool Restock(ItemStack from, Player who = null)
    {
        if (from.Empty || from.TopType != Item.Carton || Stock >= Cap || (who && !who.Tick())) return false;
        var t = from.Pop(out _);
        int slot = Stock;
        Stock++;
        var target = SlotBoxes[Mathf.Min(slot, SlotBoxes.Count - 1)].transform;
        SlotBoxes[Mathf.Min(slot, SlotBoxes.Count - 1)].SetActive(false);
        Fly.To(t, target, Vector3.zero, 0.22f, () => ItemPool.Release(Item.Carton, t));
        Sfx.I.Drop();
        return true;
    }

    // Picking takes a beat per order: scan the slot, drop the unit in a tote.
    public bool Pick(ItemStack into, Player who, float dt, float rate = 1f)
    {
        if (Stock <= 0 || Game.I.Orders <= 0 || !into.CanAccept(Item.Tote)) { pickT = 0; return false; }
        pickT += dt * rate;
        if (pickT < Game.I.PickTime) return false;
        pickT = 0;
        Stock--; Game.I.Orders--;
        var from = SlotBoxes[Mathf.Clamp(Stock, 0, SlotBoxes.Count - 1)].transform.position;
        into.Add(Item.Tote, from);
        Sfx.I.Scan();
        return true;
    }
}

// ======================================================================
// 05 Pack line: totes ride the infeed belt to the packing table; parcels ride out into a cart.
// ======================================================================
public class PackLine : MonoBehaviour
{
    class OnBelt { public Transform t; public Item type; public float u; }
    readonly List<OnBelt> infeed = new List<OnBelt>(), outfeed = new List<OnBelt>();
    public Vector3 InA, InB, OutA, OutB;   // belt ends (world)
    public ItemStack Cart;
    public Zone DropZone, PackZone, CartZone;
    public Sign DropCard, PackCard, CartCard, NeedsPacker, CartLabel;
    public Transform Screen;
    public bool PackerBot;
    public int InfeedCap = 8;
    float packT;
    const float Spacing = 0.62f;

    public int OnInfeed => infeed.Count;
    public bool Packing { get; private set; }

    void Update()
    {
        float dt = Time.deltaTime;
        var p = Game.I.Player;
        if (DropZone.PlayerIn && Drop(p.Stack, p)) Game.I.Tutorial(TutStep.DropTotes);
        if (CartZone.PlayerIn && TakeParcels(p.Stack, p)) Game.I.Tutorial(TutStep.TakeParcels);

        MoveBelt(infeed, InA, InB, dt);
        MoveBelt(outfeed, OutA, OutB, dt);
        // parcels reaching the end of the outfeed drop into the cart
        if (outfeed.Count > 0 && outfeed[0].u >= 0.999f && !Cart.Full) { var b = outfeed[0]; outfeed.RemoveAt(0); Cart.Push(Item.Parcel, b.t); Sfx.I.Drop(); }

        // packing needs someone at the table
        bool packer = PackZone.PlayerIn || PackerBot;
        bool ready = infeed.Count > 0 && infeed[0].u >= 0.999f && Cart.Count + outfeed.Count < Cart.Capacity;
        Packing = packer && ready;
        if (Packing)
        {
            packT += dt * Game.I.PackSpeed;
            if (Screen) Screen.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 20f) * 0.02f);
            if (packT >= 1f)
            {
                packT = 0;
                var b = infeed[0]; infeed.RemoveAt(0);
                ItemPool.Release(Item.Tote, b.t);
                var parcel = ItemPool.Get(Item.Parcel);
                parcel.position = OutA;
                outfeed.Add(new OnBelt { t = parcel, type = Item.Parcel, u = 0 });
                Sfx.I.Tape();
                if (PackZone.PlayerIn) Game.I.Tutorial(TutStep.Pack);
                Game.I.Save.packed++;
            }
        }
        else packT = Mathf.Max(0, packT - dt * 0.5f);

        DropCard.Set(infeed.Count + " ON INFEED");
        DropCard.Bar(infeed.Count / (float)InfeedCap);
        PackCard.Bar(Packing ? packT : 0f);
        CartCard.Bar(Cart.Count / (float)Cart.Capacity);
        CartLabel.Bar(Cart.Count / (float)Cart.Capacity);
        PackCard.Set(Packing ? "PACKING..." : PackerBot ? "PACKER ON DUTY" : "STAND TO PACK", Packing);
        CartCard.Set(Cart.Count + " / " + Cart.Capacity + " BOXES", Cart.Full);
        CartLabel.Set("CART " + Cart.Count + "/" + Cart.Capacity);
        NeedsPacker.Visible = infeed.Count > 0 && !packer;
        PackZone.Progress = Packing ? packT : -1f;
    }

    void MoveBelt(List<OnBelt> belt, Vector3 a, Vector3 b, float dt)
    {
        float len = Vector3.Distance(a, b);
        for (int i = 0; i < belt.Count; i++)
        {
            var e = belt[i];
            float max = 1f - i * Spacing / len;
            e.u = Mathf.Min(max, e.u + dt * 1.3f / len);
            var pos = Vector3.Lerp(a, b, Mathf.Max(0, e.u));
            e.t.position = Vector3.Lerp(e.t.position, pos, 1f - Mathf.Exp(-dt * 18f));
            e.t.rotation = Quaternion.identity;
        }
    }

    public bool Drop(ItemStack from, Player who = null)
    {
        if (from.Empty || from.TopType != Item.Tote || infeed.Count >= InfeedCap || (who && !who.Tick())) return false;
        var t = from.Pop(out _);
        t.position = Vector3.Lerp(t.position, InA, 0.5f) + Vector3.up * 0.4f;
        infeed.Add(new OnBelt { t = t, type = Item.Tote, u = -0.15f });
        Sfx.I.Drop();
        return true;
    }

    public bool TakeParcels(ItemStack into, Player who = null)
    {
        if (Cart.Empty || !into.CanAccept(Item.Parcel) || (who && !who.Tick())) return false;
        Cart.TransferTo(into);
        Sfx.I.Pop();
        return true;
    }

    public void Clear()
    {
        foreach (var b in infeed) ItemPool.Release(b.type, b.t);
        foreach (var b in outfeed) ItemPool.Release(b.type, b.t);
        infeed.Clear(); outfeed.Clear();
    }

    public string SaveString()
    {
        int totes = infeed.Count, parcels = outfeed.Count;
        return totes + "," + parcels;
    }
    public void Restore(int totes, int parcels)
    {
        for (int i = 0; i < totes && infeed.Count < InfeedCap; i++) { var t = ItemPool.Get(Item.Tote); t.position = InB; infeed.Add(new OnBelt { t = t, type = Item.Tote, u = 1 }); }
        for (int i = 0; i < parcels; i++) { var t = ItemPool.Get(Item.Parcel); t.position = OutB; outfeed.Add(new OnBelt { t = t, type = Item.Parcel, u = 1 }); }
    }
}

// ======================================================================
// 06 Sort: drop parcels in the chute, the robot arm sorts them into bins A-D; forklift full bins out.
// ======================================================================
public class Sorter : MonoBehaviour
{
    public ItemStack Chute;
    public Transform[] BinSlots = new Transform[4];
    public readonly Transform[] Bins = new Transform[4];
    public Sign[] BinLabels = new Sign[4];
    public Zone DropZone, LiftZone;
    public Sign DropCard, LiftCard, BringCart;
    public Transform Arm;
    float sortT; int nextBin; float armSwing;
    readonly int[] inFlight = new int[4];
    static readonly string[] Names = { "A", "B", "C", "D" };

    public int InBins { get { int n = 0; foreach (var b in Bins) if (b) n += b.GetComponent<Payload>().Count; return n; } }

    void Start()
    {
        for (int i = 0; i < 4; i++) if (!Bins[i]) NewBin(i, 0);
    }

    public void NewBin(int i, int count)
    {
        var g = ItemPool.Get(Item.Gaylord);
        g.SetParent(BinSlots[i], false);
        g.localPosition = Vector3.zero; g.localRotation = Quaternion.identity; g.localScale = Vector3.one;
        g.GetComponent<Payload>().Set(count);
        Bins[i] = g;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        var p = Game.I.Player;
        if (DropZone.PlayerIn && Drop(p.Stack, p)) Game.I.Tutorial(TutStep.Sort);
        if (LiftZone.PlayerIn && TakeBin(p.Stack, p)) Game.I.Tutorial(TutStep.TakeBin);

        // robot arm sorts the chute into the bin with room (round robin = different planets)
        if (!Chute.Empty && (sortT += dt) >= Game.I.SortTime)
        {
            for (int k = 0; k < 4; k++)
            {
                int i = (nextBin + k) % 4;
                var load = Bins[i] ? Bins[i].GetComponent<Payload>() : null;
                if (load == null || load.Count + inFlight[i] >= load.Max) continue;
                sortT = 0; nextBin = (i + 1) % 4;
                var t = Chute.Pop(out _);
                inFlight[i]++;
                int bin = i;
                Fly.To(t, Bins[i], new Vector3(0, 0.6f, 0), 0.3f, () =>
                {
                    ItemPool.Release(Item.Parcel, t);
                    inFlight[bin] = Mathf.Max(0, inFlight[bin] - 1);
                    load.Set(load.Count + 1);   // lands in whichever bin it was aimed at, even if that bin just left on a forklift
                });
                armSwing = 1f;
                Sfx.I.Scan();
                break;
            }
        }
        if (Arm)
        {
            armSwing = Mathf.MoveTowards(armSwing, 0, dt * 3f);
            Arm.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 6f) * 40f * armSwing + nextBin * 30f - 45f, 0);
        }

        int total = InBins;
        DropCard.Set(Chute.Count + " TO SORT");
        DropCard.Bar(Chute.Count / (float)Chute.Capacity);
        LiftCard.Set(total + " IN BINS", FullestCount() >= 8);
        LiftCard.Bar(FullestCount() / 16f);
        for (int i = 0; i < 4; i++)
        {
            int c = Bins[i] ? Bins[i].GetComponent<Payload>().Count : 0;
            BinLabels[i].Set("BIN " + Names[i] + "  " + c + "/16");
            BinLabels[i].Bar(c / 16f);
        }
        // nudge: parcels are piling up in the cart while the sorter sits idle
        BringCart.Visible = Game.I.Save.tut > (int)TutStep.TakeParcels && Chute.Empty && Game.I.Pack.Cart.Count >= 6 && (p.Stack.Empty || p.Stack.TopType != Item.Parcel);
    }

    int FullestCount() { int m = 0; foreach (var b in Bins) if (b) m = Mathf.Max(m, b.GetComponent<Payload>().Count); return m; }

    public bool Drop(ItemStack from, Player who = null)
    {
        if (from.Empty || from.TopType != Item.Parcel || Chute.Full || (who && !who.Tick())) return false;
        from.TransferTo(Chute);
        Sfx.I.Drop();
        return true;
    }

    public bool TakeBin(ItemStack into, Player who = null)
    {
        if (!into.CanAccept(Item.Gaylord)) return false;
        int best = -1, bc = 0;
        for (int i = 0; i < 4; i++) { int c = Bins[i] && inFlight[i] == 0 ? Bins[i].GetComponent<Payload>().Count : 0; if (c > bc) { bc = c; best = i; } }
        if (best < 0 || (who && !who.Tick())) return false;
        var g = Bins[best];
        g.SetParent(null, true);
        into.Push(Item.Gaylord, g);
        NewBin(best, 0);
        Sfx.I.Lift();
        return true;
    }
}

// ======================================================================
// 07 Outbound dock: load bins on the space truck and get paid per box.
// ======================================================================
public class OutboundDock : MonoBehaviour
{
    public Zone Zone;
    public CashPile Cash;
    public Sign Header, Card;
    public Transform Truck, Bay;
    public Vector3 TruckDock, TruckAway;
    int loaded; float leaveT; int state;   // 0 docked, 1 leaving, 2 returning

    void Update()
    {
        float dt = Time.deltaTime;
        var p = Game.I.Player;
        if (Zone.PlayerIn && Ship(p.Stack, p)) Game.I.Tutorial(TutStep.Ship);
        Card.Set(Game.I.Save.shipped + " SHIPPED · " + Kit.Money(Game.I.BoxPrice) + " / BOX");

        if (Truck)
        {
            switch (state)
            {
                case 0: Truck.position = Vector3.Lerp(Truck.position, TruckDock, 1f - Mathf.Exp(-dt * 6f)); if (loaded >= 4) { state = 1; leaveT = 0; Sfx.I.Whoosh(); } break;
                case 1: leaveT += dt / 1.3f; Truck.position = Vector3.Lerp(TruckDock, TruckAway, leaveT * leaveT); if (leaveT >= 1f) { state = 2; leaveT = 0; loaded = 0; } break;
                case 2: leaveT += dt / 1.5f; Truck.position = Vector3.Lerp(TruckAway, TruckDock, 1f - Mathf.Pow(1f - Mathf.Clamp01(leaveT), 3)); if (leaveT >= 1f) state = 0; break;
            }
        }
    }

    public bool Ship(ItemStack from, Player who = null)
    {
        if (from.Empty || from.TopType != Item.Gaylord || (who && !who.Tick())) return false;
        var t = from.Pop(out _);
        var load = t.GetComponent<Payload>();
        int boxes = load.Count;
        double pay = boxes * Game.I.BoxPrice;
        Fly.To(t, Bay, Vector3.up * 0.5f, 0.35f, () => { load.Set(0); ItemPool.Release(Item.Gaylord, t); });
        Cash.Deposit(pay, Bay.position + Vector3.up);
        Game.I.Save.shipped += boxes;
        Game.I.Shipped(boxes, pay);
        loaded++;
        Sfx.I.Register();
        UI.I.FloatText(Bay.position + Vector3.up * 2f, "+" + Kit.Money(pay) + "  (" + boxes + " BOXES)", Kit.Hex("#8cff9e"), 50);
        return true;
    }
}

// ======================================================================
// Power Pack: a bonus station. Stand on it when it's charged for 60 s of double-speed packing and double pay.
// ======================================================================
public class PowerPack : MonoBehaviour
{
    public Zone Zone;
    public Sign Card;
    float stay;

    void Update()
    {
        var g = Game.I;
        if (Zone.PlayerIn && g.PowerReady)
        {
            stay += Time.deltaTime;
            if (stay > 0.5f) { stay = -99f; g.RequestPower(); }
        }
        else if (!Zone.PlayerIn) stay = 0;
        Card.Set(g.PowerLeft > 0 ? "ACTIVE  " + Mathf.CeilToInt(g.PowerLeft) + "s" : g.PowerReady ? (WebBridge.AdsAvailable ? (WebBridge.AdFree ? "BONUS READY · FREE" : "BONUS READY · AD") : "BONUS READY") : "RECHARGING " + UI.Clock(g.PowerCooldown), g.PowerReady);
        Zone.Color = g.PowerLeft > 0 ? Kit.Hex("#8cff9e") : Kit.Hex("#ff5c9a");
    }
}

// ======================================================================
// Shared: cash pile, unlock pad, upgrade terminal
// ======================================================================
public class CashPile : MonoBehaviour
{
    public ItemStack Pile;
    public Zone Zone;
    double value;
    const int MaxBills = 48;

    public void Deposit(double amount, Vector3 from)
    {
        if (Game.I.AutoBank) { Game.I.AddMoney(amount, true); Sfx.I.Coin(); return; }
        value += amount;
        int bills = Mathf.Clamp(Mathf.CeilToInt((float)(amount / Game.I.BillValue)), 1, 6);
        for (int i = 0; i < bills && Pile.Count < MaxBills; i++) Pile.Add(Item.Cash, from, i * 0.05f);
        if (Pile.Empty) Pile.Add(Item.Cash, from);
    }

    void Update()
    {
        var p = Game.I.Player;
        if (Zone.PlayerIn && !Pile.Empty && p.CashTick())
        {
            int n = Pile.Count;
            double share = value / n;
            value -= share;
            var t = Pile.Pop(out _);
            Fly.To(t, p.transform, new Vector3(0, 0.8f, 0), 0.25f, () => ItemPool.Release(Item.Cash, t));
            Game.I.AddMoney(share, true);
            Sfx.I.Coin();
            UI.I.CashFly(p.transform.position);
            WebBridge.Vibrate(8);
            Game.I.Tutorial(TutStep.Collect);
            if (Pile.Empty) value = 0;
        }
    }

    public double Value => value;
}

public class Pad : MonoBehaviour
{
    public string Id;
    public double Cost, Paid;
    public string Label;
    public Action OnDone;
    public Zone Zone;
    public WorldTag Tag;
    float stay, coinT;
    public bool Done;

    void Update()
    {
        if (Done) return;
        Zone.Progress = (float)(Paid / Cost);
        if (Tag != null) { Tag.Set(Label, Kit.Money(Math.Ceiling(Cost - Paid))); Tag.Progress = (float)(Paid / Cost); }
        if (!Zone.PlayerIn) { stay = 0; return; }
        stay += Time.deltaTime;
        if (stay < 0.25f || Game.I.Money < 1) return;

        double rate = Math.Max(Cost * Time.deltaTime / 1.4, 1.0);
        double pay = Math.Min(Math.Min(rate, Game.I.Money), Cost - Paid);
        Game.I.AddMoney(-pay, false);
        Paid += pay;
        coinT -= Time.deltaTime;
        if (coinT <= 0)
        {
            coinT = 0.06f;
            var t = ItemPool.Get(Item.Cash);
            t.position = Game.I.Player.transform.position + Vector3.up * 1.2f;
            Fly.To(t, transform, Vector3.up * 0.1f, 0.3f, () => ItemPool.Release(Item.Cash, t));
            Sfx.I.Coin();
        }
        Game.I.SavePad(Id, Paid);
        if (Paid >= Cost - 0.001)
        {
            Done = true;
            Game.I.CompletePad(this);
        }
    }
}

public class Terminal : MonoBehaviour
{
    public Zone Zone;
    float stay; bool opened;
    void Update()
    {
        if (Zone.PlayerIn)
        {
            stay += Time.deltaTime;
            if (stay > 0.35f && !opened) { opened = true; UI.I.OpenUpgrades(); }
        }
        else
        {
            if (opened) UI.I.CloseUpgrades();
            stay = 0; opened = false;
        }
    }
}
