using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Advertisements;

public class RewardedAdsUnity : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
   
   
    private void Start()
    {
#if UNITY_EDITOR
        string adUnitId = "unused";
#elif UNITY_ANDROID
        string adUnitId = AdsManager.instance.UnityRewardedID;
#elif UNITY_IPHONE
        string adUnitId = AdsManager.instance.UnityRewardedID;
#else
        string adUnitId = "unexpected_platform";
#endif
        LoadAd();
    }

    // Load content to the Ad Unit:
    public void LoadAd()
    {
        // IMPORTANT! Only load content AFTER initialization (in this example, initialization is handled in a different script).
        Debug.Log("Loading Ad: " + AdsManager.instance.UnityRewardedID);
        Advertisement.Load(AdsManager.instance.UnityRewardedID, this);
    }

    // If the ad successfully loads, add a listener to the button and enable it:
    public void OnUnityAdsAdLoaded(string adUnitId)
    {
        Debug.Log("Ad Loaded: " + adUnitId);

        if (adUnitId.Equals(AdsManager.instance.UnityRewardedID))
        {
            
        }
    }

    // Implement a method to execute when the user clicks the button:
    public void ShowAd()
    {
        
        // Then show the ad:
        Advertisement.Show(AdsManager.instance.UnityRewardedID, this);
    }

    // Implement the Show Listener's OnUnityAdsShowComplete callback method to determine if the user gets a reward:
    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        if (adUnitId.Equals(AdsManager.instance.UnityRewardedID) && showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
        {
            Debug.Log("Unity Ads Rewarded Ad Completed");
            // Grant a reward.
            RewardHandler.instance.GiveReward();

            // Load another ad:
            Advertisement.Load(AdsManager.instance.UnityRewardedID, this);
        }
    }

    // Implement Load and Show Listener error callbacks:
    public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
    {
        Debug.Log($"Error loading Ad Unit {adUnitId}: {error.ToString()} - {message}");
        // Use the error details to determine whether to try to load another ad.
    }

    public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
    {
        Debug.Log($"Error showing Ad Unit {adUnitId}: {error.ToString()} - {message}");
        // Use the error details to determine whether to try to load another ad.
    }

    public void OnUnityAdsShowStart(string adUnitId) { }
    public void OnUnityAdsShowClick(string adUnitId) { }

    void OnDestroy()
    {
        // Clean up the button listeners:
    }
}