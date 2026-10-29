using UnityEngine;
using UnityEngine.UI;

// Phone title screen: the ORBIT DEPOT logo over the live floor, PLAY, SETTINGS and NO ADS.
// The depot is already built and running behind it; PLAY just fades the screen away.
public partial class UI
{
    RectTransform titleUI, titleSettings, titlePlay, titlePlanet;
    readonly System.Collections.Generic.List<Image> titleStars = new System.Collections.Generic.List<Image>();
    Button titleNoAds, titleSet;
    Text setSound, setMusic, setVibe;
    Button setRestore, setPrivacy;
    public bool TitleUp => titleUI != null;

    void ShowTitle()
    {
        titleUI = Fill("title", root);
        var shade = titleUI.gameObject.AddComponent<Image>(); shade.color = new Color(0.02f, 0.035f, 0.08f, 0.88f);
        // a twinkling starfield over the floor
        var rnd = new System.Random(7);
        titleStars.Clear();
        for (int i = 0; i < 46; i++)
        {
            var st = Img(titleUI, HudArt.Disc, new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble()), Vector2.zero, Vector2.one * (6 + rnd.Next(10)));
            st.color = i % 5 == 0 ? Cyan : Color.white;
            titleStars.Add(st);
        }
        var ts = Fill("safe", titleUI); ts.gameObject.AddComponent<SafeArea>();

        // logo: a small planet with a ring orbiting above the wordmark
        var glow = Img(ts, HudArt.Glow, new Vector2(.5f, .5f), new Vector2(0, 380), new Vector2(1300, 900)); glow.color = Kit.A(Cyan, 0.22f);
        titlePlanet = Rect("planet", ts, new Vector2(.5f, .5f), new Vector2(0, 690), new Vector2(170, 170));
        var ball = Img(titlePlanet, HudArt.Disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 120)); ball.color = Orange;
        var ring = Img(titlePlanet, HudArt.RingS, new Vector2(.5f, .5f), Vector2.zero, new Vector2(230, 70)); ring.color = Gold; ring.preserveAspect = false;   // a flat ellipse around the planet
        var a = Txt(ts, "ORBIT", 190, new Vector2(.5f, .5f), new Vector2(0, 450), Color.white); Shadow(a, 7);
        var b = Txt(ts, "DEPOT", 190, new Vector2(.5f, .5f), new Vector2(0, 270), Cyan); Shadow(b, 7);
        Txt(ts, "RECEIVE · PICK · PACK · SHIP", 38, new Vector2(.5f, .5f), new Vector2(0, 140), Soft);

        // buttons
        titlePlay = (RectTransform)Button(ts, "PLAY", new Vector2(.5f, .5f), new Vector2(0, -420), new Vector2(620, 190), Kit.Hex("#35C46A"), Color.white, StartGame, out var playText).transform;
        playText.fontSize = 96; Shadow(playText, 4);
        titleSet = Button(ts, "SETTINGS", new Vector2(.5f, .5f), new Vector2(-165, -620), new Vector2(310, 120), new Color(1, 1, 1, 0.14f), Color.white, OpenSettings, out var setText);
        setText.fontSize = 34; setText.rectTransform.anchoredPosition = new Vector2(26, 0);
        Img(titleSet.transform, HudArt.Gear, new Vector2(0, .5f), new Vector2(46, 2), new Vector2(44, 44));
        titleNoAds = Button(ts, "", new Vector2(.5f, .5f), new Vector2(165, -620), new Vector2(310, 120), Gold, Ink, Ads.BuyRemoveAds, out var noAdsText);
        noAdsText.fontSize = 34;
        Txt(ts, "v" + Application.version, 24, new Vector2(.5f, 0), new Vector2(0, 40), Kit.A(Color.white, 0.35f));

        RefreshTitle();
        Ads.Changed += RefreshTitle;
        Sfx.I.StartMusic();   // no tap needed on phones (the browser build waits for one)
        StartCoroutine(TitleLoop(a, b));
    }

    void RefreshTitle()
    {
        if (!titleUI) return;
        bool noAds = !Ads.AdFree && Ads.CanBuyRemoveAds;
        titleNoAds.gameObject.SetActive(noAds);
        titleNoAds.GetComponentInChildren<Text>().text = "NO ADS  " + Ads.RemoveAdsPrice;
        // SETTINGS sits centred when NO ADS isn't on sale
        ((RectTransform)titleSet.transform).anchoredPosition = new Vector2(noAds ? -165 : 0, -620);
        RefreshSettings();
    }

    System.Collections.IEnumerator TitleLoop(Text a, Text b)
    {
        float k = 0;
        while (titleUI)
        {
            k += Time.unscaledDeltaTime;
            float pop = Kit.EaseOutBack(Mathf.Clamp01(k / 0.5f));
            a.rectTransform.localScale = b.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, pop);
            if (titlePlanet)
            {
                titlePlanet.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Kit.EaseOutBack(Mathf.Clamp01((k - 0.2f) / 0.5f)));
                titlePlanet.anchoredPosition = new Vector2(0, 690 + Mathf.Sin(k * 2f) * 10f);
                titlePlanet.localRotation = Quaternion.Euler(0, 0, -14 + Mathf.Sin(k * 1.3f) * 6f);
            }
            if (titlePlay && !titleSettings) titlePlay.localScale = Vector3.one * (1f + Mathf.Sin(k * 4f) * 0.035f);
            for (int i = 0; i < titleStars.Count; i++)
                titleStars[i].canvasRenderer.SetAlpha(0.35f + 0.65f * Mathf.Abs(Mathf.Sin(k * (0.6f + i % 7 * 0.15f) + i)));
            yield return null;
        }
    }

    void StartGame()
    {
        if (!titleUI) return;
        Ads.Changed -= RefreshTitle;
        var t = titleUI; titleUI = null;   // gameplay input starts this frame
        Sfx.I.Whoosh();
        StartCoroutine(FadeOut(t));
    }

    System.Collections.IEnumerator FadeOut(RectTransform t)
    {
        var group = t.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        for (float k = 0; k < 1f; k += Time.unscaledDeltaTime / 0.3f)
        {
            group.alpha = 1f - k;
            t.localScale = Vector3.one * (1f + k * 0.08f);
            yield return null;
        }
        Destroy(t.gameObject);
    }

    // ---------------- settings ----------------
    void OpenSettings()
    {
        if (titleSettings) return;
        Sfx.I.Click();
        titleSettings = Fill("settings", titleUI);
        var dim = titleSettings.gameObject.AddComponent<Image>(); dim.color = new Color(0.01f, 0.02f, 0.06f, 0.7f);
        var ss = Fill("safe", titleSettings); ss.gameObject.AddComponent<SafeArea>();
        // the card fits the rows that show: RESTORE (iPhone, until the pass is owned) and PRIVACY (where the law asks for it)
        int extra = (Ads.ShowRestoreButton ? 1 : 0) + (Ads.PrivacyOptionsRequired ? 1 : 0);
        var card = PanelBox(ss, new Vector2(.5f, .5f), new Vector2(0, -20), new Vector2(860, 680 + extra * 130), Kit.A(Cyan, 0.45f), 0.97f);
        var title = Txt(card, "SETTINGS", 64, new Vector2(.5f, 1), new Vector2(0, -90), Color.white); Shadow(title, 3);
        Button(card, "X", new Vector2(1, 1), new Vector2(-70, -74), new Vector2(84, 84), new Color(1, 1, 1, 0.1f), Color.white, CloseSettings, out _);

        Button(card, "", new Vector2(.5f, 1), new Vector2(0, -250), new Vector2(640, 120), new Color(1, 1, 1, 0.12f), Color.white, () =>
        {
            ToggleSound(); RefreshSettings(); Game.I.Persist();
        }, out setSound);
        Button(card, "", new Vector2(.5f, 1), new Vector2(0, -400), new Vector2(640, 120), new Color(1, 1, 1, 0.12f), Color.white, () =>
        {
            Sfx.I.SetMusic(Sfx.MusicOff); RefreshSettings();
        }, out setMusic);
        Button(card, "", new Vector2(.5f, 1), new Vector2(0, -550), new Vector2(640, 120), new Color(1, 1, 1, 0.12f), Color.white, () =>
        {
            PlayerPrefs.SetInt("haptics_off", PlayerPrefs.GetInt("haptics_off", 0) == 1 ? 0 : 1); PlayerPrefs.Save();
            Mobile.Vibrate(30); RefreshSound(); RefreshSettings();
        }, out setVibe);
        setSound.fontSize = setMusic.fontSize = setVibe.fontSize = 40;

        setRestore = Button(card, "RESTORE PURCHASES", new Vector2(.5f, 1), new Vector2(0, -720), new Vector2(640, 110), new Color(1, 1, 1, 0.08f), Soft, Ads.RestorePurchases, out var restoreText);
        setPrivacy = Button(card, "PRIVACY", new Vector2(.5f, 1), new Vector2(0, Ads.ShowRestoreButton ? -850 : -720), new Vector2(640, 110), new Color(1, 1, 1, 0.08f), Soft, Ads.ShowPrivacyOptions, out var privacyText);
        restoreText.fontSize = privacyText.fontSize = 32;

        RefreshSettings();
        StartCoroutine(PopIn(card));
    }

    void CloseSettings()
    {
        if (titleSettings) { Sfx.I.Click(); Destroy(titleSettings.gameObject); }
        titleSettings = null;
    }

    void RefreshSettings()
    {
        if (!titleSettings) return;
        setSound.text = Game.I.Save.muted ? "SOUND: OFF" : "SOUND: ON";
        setMusic.text = Sfx.MusicOff ? "MUSIC: OFF" : "MUSIC: ON";
        setVibe.text = PlayerPrefs.GetInt("haptics_off", 0) == 1 ? "VIBRATION: OFF" : "VIBRATION: ON";
        setRestore.gameObject.SetActive(Ads.ShowRestoreButton);
        setPrivacy.gameObject.SetActive(Ads.PrivacyOptionsRequired);
    }
}
