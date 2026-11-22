using System;
using System.Collections;
using MangoMango.Advertisement;
using UnityEngine;

namespace MangoMango.IAP
{
    /// <summary>
    /// The single coin wallet for the project: store purchases, the rewarded placement and every
    /// money label read and write through here. It lives on the persistent IAP Manager prefab, so
    /// the balance survives scene loads, and it stores to PlayerPrefs("Coins") - the same key
    /// ApplovinAdManager.GrantRewardCoins already writes to, so rewarded and purchased coins share
    /// one balance instead of two.
    /// </summary>
    public class CoinManager : MonoBehaviour
    {
        public const string CoinsKey = "Coins";

        // The Best Bundle is a non-consumable that also pays coins. iOS delivers it again on every
        // restore, so the payout is latched and can never be granted twice.
        private const string BestBundleCoinsGrantedKey = "BestBundleCoinsGranted";

        public static CoinManager Instance { get; private set; }

        /// <summary>Raised with the new balance every time the wallet changes.</summary>
        public static event Action<int> OnCoinsChanged;

        [Header("Coins granted per purchased product")]
        [SerializeField] private int coins1Amount = 1000;
        [SerializeField] private int coins2Amount = 2000;
        [SerializeField] private int coins3Amount = 4000;
        [SerializeField] private int coins4Amount = 8000;
        [SerializeField] private int bestBundleCoins = 5000;

        public static int Balance => PlayerPrefs.GetInt(CoinsKey, 0);

        #region Unity Methods
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.onPurchaseSuccess += HandlePurchaseSuccess;
            }

            // Rewarded coins are written by ApplovinAdManager itself; this only repaints the labels.
            ApplovinAdManager.OnRewardedAdCoinsGranted += HandleRewardedCoinsGranted;
        }

        private void OnDestroy()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.onPurchaseSuccess -= HandlePurchaseSuccess;
            }

            ApplovinAdManager.OnRewardedAdCoinsGranted -= HandleRewardedCoinsGranted;
        }
        #endregion

        #region Wallet
        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;

            int updated = Balance + amount;
            PlayerPrefs.SetInt(CoinsKey, updated);
            PlayerPrefs.Save();
            RaiseChanged(updated);
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;

            int balance = Balance;
            if (balance < amount) return false;

            int updated = balance - amount;
            PlayerPrefs.SetInt(CoinsKey, updated);
            PlayerPrefs.Save();
            RaiseChanged(updated);
            return true;
        }

        /// <summary>Repaints every money label from the saved balance.</summary>
        public static void RefreshBalance() => RaiseChanged(Balance);

        private static void RaiseChanged(int balance)
        {
            try
            {
                OnCoinsChanged?.Invoke(balance);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CoinManager] Coin label update failed: {e.Message}");
            }
        }
        #endregion

        #region Purchase / Reward Payouts
        private int GetCoinsFor(ProductName productName)
        {
            switch (productName)
            {
                case ProductName.Coins1: return coins1Amount;
                case ProductName.Coins2: return coins2Amount;
                case ProductName.Coins3: return coins3Amount;
                case ProductName.Coins4: return coins4Amount;
                case ProductName.BestBundle: return bestBundleCoins;
                default: return 0; // Remove Ads pays no coins
            }
        }

        private void HandlePurchaseSuccess(ProductName productName)
        {
            int amount = GetCoinsFor(productName);
            if (amount <= 0) return;

            if (productName == ProductName.BestBundle)
            {
                if (PlayerPrefs.GetInt(BestBundleCoinsGrantedKey, 0) == 1)
                {
                    Debug.Log("[CoinManager] Best Bundle coins already paid out once - restore ignored.");
                    return;
                }

                PlayerPrefs.SetInt(BestBundleCoinsGrantedKey, 1);
                PlayerPrefs.Save();
            }

            AddCoins(amount);
            Debug.Log($"[CoinManager] <color=#FFD700>{productName}</color> purchased -> +{amount} coins (balance {Balance}).");
        }

        private void HandleRewardedCoinsGranted(int amount)
        {
            // ApplovinAdManager raises this before it writes the balance, so the labels are refreshed
            // on the next frame - after the write - instead of showing the previous total.
            StartCoroutine(RefreshNextFrame());
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            RefreshBalance();
        }
        #endregion
    }
}
