// Rewarded ads on the Play build (AdMob, rewarded only) plus the "Remove Ads" pass, which grants
// rewards without watching. A stub for now: the real AdMob + IAP version replaces this file, so the
// game only ever touches these three members. The web build keeps its portal ads through WebBridge.
public static class Ads
{
    public static bool Available => false;

    public static bool AdFree => false;

    public static void ShowRewarded(System.Action<bool> done) => done?.Invoke(AdFree);
}
