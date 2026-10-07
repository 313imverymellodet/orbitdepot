using System.Collections.Generic;
using UnityEngine;

// Everything that moves through the depot.
//   Carton  - a loose inbound carton (trucks drop them, the dock crew palletises them, restockers rack them)
//   Pallet  - 8 cartons on a pallet (carried one at a time)
//   Tote    - a picked order in a blue tote
//   Parcel  - a packed, labelled shipping box
//   Gaylord - a big sort bin full of parcels (forklift load)
public enum Item { Carton, Pallet, Tote, Parcel, Gaylord, Cash }

public static class Items
{
    // Heavy loads ride alone: you carry one pallet or one bin at a time.
    public static bool Bulky(Item i) => i == Item.Pallet || i == Item.Gaylord;

    public static float Scale(Item i) => 1f;

    // Vertical spacing when stacked.
    public static float Height(Item i) => i switch
    {
        Item.Carton => 0.42f, Item.Tote => 0.3f, Item.Parcel => 0.3f, Item.Pallet => 0.95f, Item.Gaylord => 1.0f, Item.Cash => 0.06f, _ => 0.3f,
    };

    public static string Name(Item i) => i switch
    {
        Item.Carton => "cartons", Item.Pallet => "pallet", Item.Tote => "totes", Item.Parcel => "parcels", Item.Gaylord => "bin", _ => "",
    };
}

// A pallet or a bin remembers how many boxes it holds and shows that many.
public class Payload : MonoBehaviour
{
    public int Count;
    public int Max = 8;
    public readonly List<GameObject> Boxes = new List<GameObject>();
    public void Set(int n)
    {
        Count = Mathf.Clamp(n, 0, Max);
        for (int i = 0; i < Boxes.Count; i++) Boxes[i].SetActive(i < Count);
    }
}

// Pools item visuals so the stacking juggle never allocates.
public static class ItemPool
{
    static readonly Dictionary<Item, Stack<Transform>> free = new Dictionary<Item, Stack<Transform>>();
    static Transform bin;
    static Material cashMat, woodMat, toteMat, toteInner, binMat, labelMat;

    public static Transform Get(Item i)
    {
        if (!bin) { bin = new GameObject("ItemPool").transform; bin.gameObject.SetActive(false); }
        if (free.TryGetValue(i, out var s) && s.Count > 0)
        {
            var t = s.Pop();
            t.SetParent(null, true);
            t.localScale = Vector3.one * Items.Scale(i);
            t.rotation = Quaternion.identity;
            return t;
        }
        return Create(i);
    }

    public static void Release(Item i, Transform t)
    {
        if (!t) return;
        if (!free.TryGetValue(i, out var s)) free[i] = s = new Stack<Transform>();
        t.SetParent(bin, false);
        s.Push(t);
    }

    static Material Mat(ref Material m, Color c, float gloss = 0.15f)
    {
        if (!m) { m = new Material(Shader.Find("Standard")) { color = c }; m.SetFloat("_Glossiness", gloss); }
        return m;
    }

    static GameObject Cube(Transform p, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localScale = size;
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // A Kenney model fitted to a target footprint width, sitting on y=0 under its own root.
    public static Transform Model(string path, float width, Transform parent = null)
    {
        var m = Kit.Spawn(path, 1f);
        var b = Kit.WorldBounds(m);
        float s = width / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
        m.transform.localScale = Vector3.one * s;
        foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (parent) m.transform.SetParent(parent, false);
        return m.transform;
    }

    static Transform Create(Item i)
    {
        var root = new GameObject(i.ToString()).transform;
        switch (i)
        {
            case Item.Cash:
            {
                if (!cashMat) { cashMat = new Material(Shader.Find("Standard")) { mainTexture = Kit.Cash }; cashMat.SetFloat("_Glossiness", 0.1f); }
                Cube(root, new Vector3(0, 0.0275f, 0), new Vector3(0.42f, 0.055f, 0.22f), cashMat);
                break;
            }
            case Item.Carton:
                Model("Furniture/cardboardBoxClosed", 0.5f, root);
                break;
            case Item.Parcel:
            {
                var m = Model("Furniture/cardboardBoxClosed", 0.4f, root);
                // a shipping label so parcels read differently from raw cartons
                Cube(root, new Vector3(0.05f, Kit.WorldBounds(m.gameObject).size.y + 0.002f, 0), new Vector3(0.2f, 0.004f, 0.14f), Mat(ref labelMat, Color.white, 0.3f));
                break;
            }
            case Item.Tote:
            {
                var tm = Mat(ref toteMat, Kit.Hex("#2f7df2"), 0.35f); var ti = Mat(ref toteInner, Kit.Hex("#1d4f9e"));
                Cube(root, new Vector3(0, 0.02f, 0), new Vector3(0.5f, 0.04f, 0.36f), tm);
                Cube(root, new Vector3(0, 0.14f, 0.17f), new Vector3(0.5f, 0.26f, 0.03f), tm);
                Cube(root, new Vector3(0, 0.14f, -0.17f), new Vector3(0.5f, 0.26f, 0.03f), tm);
                Cube(root, new Vector3(0.24f, 0.14f, 0), new Vector3(0.03f, 0.26f, 0.36f), tm);
                Cube(root, new Vector3(-0.24f, 0.14f, 0), new Vector3(0.03f, 0.26f, 0.36f), tm);
                Cube(root, new Vector3(0, 0.2f, 0), new Vector3(0.44f, 0.02f, 0.3f), ti);   // the order inside
                break;
            }
            case Item.Pallet:
            {
                var wood = Mat(ref woodMat, Kit.Hex("#d4a96a"));
                for (int k = 0; k < 3; k++) Cube(root, new Vector3(-0.42f + k * 0.42f, 0.05f, 0), new Vector3(0.12f, 0.1f, 1.0f), wood);
                for (int k = 0; k < 5; k++) Cube(root, new Vector3(0, 0.12f, -0.42f + k * 0.21f), new Vector3(1.0f, 0.04f, 0.15f), wood);
                var pl = root.gameObject.AddComponent<Payload>(); pl.Max = 8;
                for (int k = 0; k < 8; k++)
                {
                    int layer = k / 4, c = k % 4;
                    var box = Model("Furniture/cardboardBoxClosed", 0.46f, root);
                    box.localPosition = new Vector3((c % 2 - 0.5f) * 0.48f, 0.14f + layer * 0.4f, (c / 2 - 0.5f) * 0.48f);
                    pl.Boxes.Add(box.gameObject);
                }
                pl.Set(8);
                break;
            }
            case Item.Gaylord:
            {
                var bm = Mat(ref binMat, Kit.Hex("#c08a4e"));
                var wood = Mat(ref woodMat, Kit.Hex("#d4a96a"));
                Cube(root, new Vector3(0, 0.05f, 0), new Vector3(1.0f, 0.1f, 1.0f), wood);
                Cube(root, new Vector3(0, 0.12f, 0), new Vector3(0.96f, 0.04f, 0.96f), bm);
                Cube(root, new Vector3(0, 0.5f, 0.47f), new Vector3(0.96f, 0.8f, 0.03f), bm);
                Cube(root, new Vector3(0, 0.5f, -0.47f), new Vector3(0.96f, 0.8f, 0.03f), bm);
                Cube(root, new Vector3(0.47f, 0.5f, 0), new Vector3(0.03f, 0.8f, 0.96f), bm);
                Cube(root, new Vector3(-0.47f, 0.5f, 0), new Vector3(0.03f, 0.8f, 0.96f), bm);
                var pl = root.gameObject.AddComponent<Payload>(); pl.Max = 16;
                for (int k = 0; k < 16; k++)
                {
                    int layer = k / 4, c = k % 4;
                    var p = Model("Furniture/cardboardBoxClosed", 0.36f, root);
                    p.localPosition = new Vector3((c % 2 - 0.5f) * 0.42f, 0.14f + layer * 0.24f, (c / 2 - 0.5f) * 0.42f);
                    p.localRotation = Quaternion.Euler(0, k * 23f, 0);
                    pl.Boxes.Add(p.gameObject);
                }
                pl.Set(0);
                break;
            }
        }
        return root;
    }
}
