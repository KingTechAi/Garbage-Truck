using UnityEngine;

public static class AdsOrientation
{
    // AdMob takes full-screen ad orientation from the activity at ad load time.
    // The project uses "Use OS autorotation", so the activity is not locked and can
    // still sit in the device's portrait configuration when ads initialize.
    // Runs before any scene, so it is applied before MobileAds.Initialize().
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LockLandscape()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // Android ignores a repeated orientation request, so leave and re-enter
        // AutoRotation to push a fresh landscape-only request to the activity.
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
