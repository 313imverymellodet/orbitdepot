# Publishing ORBIT DEPOT on Google Play

This repo builds both the browser game (`DepotBuild` → `dist/`) and the Android app (`AndroidBuild` → `android/`). The Android build plays offline. The website's portal ads, Sign in with Base badges and analytics are left out. Monetization is `unity/Assets/Scripts/Ads.cs`, the same file in every Mello Vibes game:

- AdMob rewarded ads, rewarded only, behind Google's consent form for EEA/UK players. They are offered for the Power Pack (75 s of 2× packing and pay) and the 2× offline bonus on the welcome-back screen.
- A one-time **Remove Ads** purchase (`remove_ads`), which works as an ad-free pass: those rewards are granted without a video.
- The pause menu has **NO ADS · price** and, where required, **PRIVACY** buttons.

With no video loaded, the Power Pack still works for free (60 s, longer recharge), as it does on the web. The phone gets a pause menu, Android back-button handling, safe areas for notches, haptics and the share sheet.

| | |
|---|---|
| App name | ORBIT DEPOT |
| Package name | `com.mellovibes.orbitdepot` (permanent once uploaded) |
| Min Android | 8.0 (API 26) |
| Target API | highest installed in Unity (Play needs the current level) |
| Architectures | ARM64 + ARMv7, IL2CPP |
| Orientation | Auto-rotate: portrait or landscape (no upside-down) |

## 1. One-time setup (Windows)

1. **Unity Hub → Installs → 6000.6.3f1 → ⚙ → Add modules → Android Build Support**, with **OpenJDK** and **Android SDK & NDK Tools** ticked.
2. **Create your upload key** (do this once and use it for all Mello Vibes apps). In PowerShell:

   ```powershell
   mkdir C:\keys
   & "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore C:\keys\mellovibes-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
   ```

   Pick a strong password and **back up the `.keystore` file and password** somewhere safe (a password manager plus a USB stick). Never commit it; `.gitignore` already blocks `*.keystore`. Google Play App Signing holds the real app signing key, so if you lose this upload key Google can reset it, but it takes days.

## 2. Build

From the repo root in PowerShell:

```powershell
$env:MV_KEYSTORE      = "C:\keys\mellovibes-upload.keystore"
$env:MV_KEYSTORE_PASS = "your-keystore-password"
$env:MV_KEY_ALIAS     = "upload"
$env:MV_KEY_PASS      = "your-keystore-password"
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -buildTarget Android -projectPath unity -executeMethod AndroidBuild.Release -versionCode 1 -quit -logFile android.log
```

- Output: `android\orbitdepot.aab`. Search `android.log` for `ANDROID BUILD OK`.
- **Every upload needs a higher `-versionCode`** (1, 2, 3…). Bump `Version` in `unity/Assets/Editor/AndroidBuild.cs` for user-visible versions (1.0.1…).
- In the editor you can also use the **Android** menu → *Build AAB for Google Play* or *Build test APK*.

### Test on your phone first

Turn on Developer options → USB debugging on the phone, plug it in, then:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -buildTarget Android -projectPath unity -executeMethod AndroidBuild.TestApk -quit -logFile android.log
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" install -r android\orbitdepot.apk
```

Check: the frame rate is smooth (60 Hz+), nothing is hidden under the notch in portrait or landscape, the **II** button pauses, the **back button** pauses during play, closes the upgrades panel and prompts, and from the pause menu goes to the home screen; switching apps pauses the game, the pause menu's sound and vibration toggles work, and Share opens the Android share sheet.

## 3. Play Console

Create the app at <https://play.google.com/console> → **Create app** → name `ORBIT DEPOT`, Game, Free.

**Store listing** (everything is in `store/`):

- App name: `ORBIT DEPOT: Space Warehouse`
- Short description: `Receive, pick, pack and ship cargo across the galaxy. Build your space depot!`
- Full description: `store/listing.md`
- App icon: `store/icon-512.png` · Feature graphic: `store/feature-graphic.png`
- Phone screenshots: `store/screenshots/*.png` (1080×1920)
- Category: **Simulation** · Tags: Simulation, Idle, Casual, Offline, Single player
- Contact email: your email · Website: `https://play.mellovibes.io`

**App content** (Policy → App content):

| Section | Answer |
|---|---|
| Privacy policy | `https://play.mellovibes.io/privacy` |
| Ads | **Yes**, the app contains ads (AdMob rewarded videos) |
| App access | All functionality is available without special access |
| Content rating (IARC questionnaire) | Everyone (no violence, no gambling, no user content) |
| Target audience | 13 and over (simplest; avoids the Families program paperwork) |
| Data safety | Advertising ID, approximate location, app interactions and diagnostics are collected and shared with Google AdMob for advertising; encrypted in transit; no account creation. |
| Government / financial / health / news | No |

**Monetization** (before the first production release):

1. AdMob → **Apps → Add app** (Android, `com.mellovibes.orbitdepot`) → create a **Rewarded** ad unit. Paste the app ID and rewarded unit ID into `AppId` / `RewardedUnitId` in `unity/Assets/Scripts/Ads.cs`. `AndroidBuild.Release` refuses Google's test IDs unless you pass `-testAds` (internal testing only).
2. Play Console → **Monetize → In-app products** → create the non-consumable `remove_ads` at $2.99 and activate it. The app must have been uploaded once (internal testing is enough) before products can be created.
3. AdMob → **Privacy & messaging** → publish a GDPR consent message for the app, so the consent form shows in the EEA/UK.

**Release:**

1. *Testing → Internal testing* → create a release → upload `orbitdepot.aab` → add yourself as a tester → install from the opt-in link.
2. **New personal developer accounts must run a closed test with at least 12 testers for 14 days** before production. Start this early: *Testing → Closed testing* with friends' Gmail addresses.
3. Then *Production → Create release*.
