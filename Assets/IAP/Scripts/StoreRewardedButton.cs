using MangoMango.Advertisement;
using UnityEngine;
using UnityEngine.UI;

namespace MangoMango.IAP
{
    /// <summary>
    /// The FREE card of the InApps store: plays a rewarded ad and pays the coins out only when the
    /// reward has actually been earned.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class StoreRewardedButton : MonoBehaviour
    {
        [Tooltip("Payout used when Remote Config is unavailable. Live value is 'SystemRewardedCoins'.")]
        [SerializeField] private int fallbackCoins = 500;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(ShowRewardedAd);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(ShowRewardedAd);
        }

        public void ShowRewardedAd()
        {
            if (ApplovinAdManager.Instance == null)
            {
                Debug.LogWarning("[CoinManager] Rewarded ad requested before the ad manager was ready.");
                return;
            }

            var remoteConfig = MangoMango.Advertisement.RemoteConfigManager.Instance;
            int coins = remoteConfig != null
                ? remoteConfig.GetInt(MangoMango.Advertisement.RCInt.SystemRewardedCoins, fallbackCoins)
                : fallbackCoins;

            ApplovinAdManager.Instance.ShowRewardedAd(() =>
            {
                CoinManager.AddCoins(coins);
                Debug.Log($"[CoinManager] Rewarded ad completed -> +{coins} coins (balance {CoinManager.Balance}).");
            });
        }
    }
}
