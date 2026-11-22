# Applovin Ad Manager

This package provides easy integration of Applovin ads and remote configuration into your Unity project.

## How to Use

1. **Add Required Prefabs to Splash Screen**
   - Drag and drop the following prefabs into your splash screen scene:
     - `Assets/Applovin Ad Manager/Prefabs/Necessary/Applovin Ad Manager (Necessary).prefab`
     - `Assets/Applovin Ad Manager/Prefabs/Necessary/Remote Config Manager (Necessary).prefab`

2. **(Optional) Enable Time Based Ads**
   - If you want to use time-based ads, also add:
     - `Assets/Applovin Ad Manager/Prefabs/Optional/Time Based Ad (Optional).prefab`

3. **Integrate IAP (In-App Purchases)**
   - Import the Unity IAP package from the Unity Package Manager.
   - After importing, if the IAP code is not activated, add the scripting define symbol `MangoMango_IAP` in **Project Settings > Player > Other Settings**.

## Exporting to New Projects

You can export this entire ads system (scripts, prefabs, MAX SDK, mediation adapters, gradle templates) as a `.unitypackage` for use in any new project.

### Export Options (Choose One):
- **Menu:** `Tools > Mango Ads > 📦 Export Ads Package...`
- **Dashboard Tab:** Open `Tools > Ad Strategy & Remote Config Dashboard` → Click the `📦 Export Package` tab.
- **Inspector:** Select any `Applovin Ad Manager` prefab → Click `📦 Export Ads Package` in the Inspector.

### Available Presets:
| Preset | What's Included |
|--------|----------------|
| **Full Ads Strategy Bundle** (Recommended) | Applovin Ad Manager, MaxSdk, EDM4U, Android Gradle Templates |
| **Monetization Suite** | Everything above + IAP System |
| **Ads Strategy Engine Only** | Only the Applovin Ad Manager scripts & prefabs |
| **Custom** | Manual selection |

### After Importing in a New Project:
Use the built-in Setup Assistant: `Tools > Mango Ads > 🚀 New Project Setup Assistant`

It will walk you through:
1. Installing UPM dependencies (`com.unity.remote-config`)
2. Running Android Resolver (EDM4U Force Resolve)
3. Adding required prefabs to your scene
4. Setting up UIScreenTracker for event-based ads

## Reward System Integration

When a rewarded ad grants coins via the Ad Strategy system, `ApplovinAdManager` fires a static event:

```csharp
// Subscribe in your game's save/economy manager:
ApplovinAdManager.OnRewardedAdCoinsGranted += (int coins) =>
{
    YourCoinManager.AddCoins(coins);
};
```

If your project has a `PlayerSaveManager` class with a static `AddCoins(int)` method, it will be called automatically via reflection. Otherwise, coins are saved to `PlayerPrefs("Coins")` as a fallback.

## Notes
- This package can be used for both Android and iOS.
- Make sure the prefabs are present in the initial scene to ensure proper initialization.
- Configure ad and remote config settings as needed in the Unity Inspector or via your remote config service.
- For IAP, ensure the scripting define symbol is set if you encounter issues with IAP code activation.

---
For more details, refer to the code comments or contact the package maintainer.
