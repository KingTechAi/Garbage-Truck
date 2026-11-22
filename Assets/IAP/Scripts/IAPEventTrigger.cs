using System;
using UnityEngine;
using UnityEngine.Events;

namespace MangoMango.IAP
{
    [Serializable]
    public class IAPEvent
    {
        public ProductName name;
        public UnityEvent onBuySuccess;
        public UnityEvent onBuyFail;
        [Tooltip("This will only triggers in start")]
        public UnityEvent onAlreadyBuyOnStart;
        
        public void InvokeAlreadyBuy()
        {
            // --- NEW: It only checks if THIS EXACT item was bought locally! ---
            bool isBoughtLocally = PlayerPrefs.GetInt("Bought_" + name.ToString(), 0) == 1;
            
            if (isBoughtLocally || IAPManager.Instance.IsPurchased(name))
            {
                onAlreadyBuyOnStart?.Invoke();
            }
        }
    }
    
    public class IAPEventTrigger : MonoBehaviour
    {
        [SerializeField] IAPEvent[] events;

        void Start()
        {
            IAPManager.Instance.onPurchaseSuccess += OnPurchaseSuccess;
            IAPManager.Instance.onPurchaseFail += OnPurchaseFail;
            
            if (IAPManager.Instance.isInitialized)
            {
                CheckAlreadyBought();
            }
            else
            {
                IAPManager.Instance.onInitializationComplete += CheckAlreadyBought;
            }
        }

        private void CheckAlreadyBought()
        {
            foreach (var item in events)
            {
                item.InvokeAlreadyBuy();
            }
        }

        private void OnPurchaseFail(ProductName name)
        {
            GetEventByName(name)?.onBuyFail?.Invoke();
        }

        private void OnPurchaseSuccess(ProductName name)
        {
            // --- NEW: Save this exact item's receipt locally to fix Editor Amnesia! ---
            PlayerPrefs.SetInt("Bought_" + name.ToString(), 1);
            PlayerPrefs.Save();
            
            GetEventByName(name)?.onBuySuccess?.Invoke();
        }
        
        IAPEvent GetEventByName(ProductName name)
        {
            foreach (var item in events)
            {
                if (item.name == name) return item;
            }
            return null;
        }
    }
}