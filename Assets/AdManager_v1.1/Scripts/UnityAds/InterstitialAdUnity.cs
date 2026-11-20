using UnityEngine;
using UnityEngine.Advertisements;

public class InterstitialAdUnity : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
   
    void Awake()
    {
        
    }
    private void Start()
    {
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.UnityInterstitialID;
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.UnityInterstitialID;
#else
        string adUnitId = "unexpected_platform";
#endif
        LoadAd();
    }

    // Load content to the Ad Unit:
    public void LoadAd()
    {
        // IMPORTANT! Only load content AFTER initialization (in this example, initialization is handled in a different script).
        Debug.Log("Loading Ad: " + AdsManager.instance.UnityInterstitialID);
        Advertisement.Load(AdsManager.instance.UnityInterstitialID, this);
    }

    // Show the loaded content in the Ad Unit:
    public void ShowAd()
    {
        // Note that if the ad content wasn't previously loaded, this method will fail
        Debug.Log("Showing Ad: " + AdsManager.instance.UnityInterstitialID);
        Advertisement.Show(AdsManager.instance.UnityInterstitialID, this);
        LoadAd();
    }

    // Implement Load Listener and Show Listener interface methods: 
    public void OnUnityAdsAdLoaded(string adUnitId)
    {
        // Optionally execute code if the Ad Unit successfully loads content.
    }

    public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
    {
        Debug.Log($"Error loading Ad Unit: {adUnitId} - {error.ToString()} - {message}");
        // Optionally execute code if the Ad Unit fails to load, such as attempting to try again.
    }

    public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
    {
        Debug.Log($"Error showing Ad Unit {adUnitId}: {error.ToString()} - {message}");
        // Optionally execute code if the Ad Unit fails to show, such as loading another ad.
    }

    public void OnUnityAdsShowStart(string adUnitId) { }
    public void OnUnityAdsShowClick(string adUnitId) { }
    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState) { }
}