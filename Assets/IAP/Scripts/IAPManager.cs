using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;


namespace MangoMango.IAP
{
    public class IAPManager : MonoBehaviour, IDetailedStoreListener
    {
        internal static IAPManager Instance;

        IStoreController storeController;
        private static IExtensionProvider storeExtensionProvider; // Store-specific features

        [SerializeField] IAPData IAPData;
        [HideInInspector] public bool isInitialized = false;
        public Action<ProductName> onPurchaseSuccess;
        public Action<ProductName> onPurchaseFail;
        public Action onInitializationComplete;

        #region Unity Methods
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            IAPData.RemoveDuplicteItems();
            SetupBuilder();
        }

        #endregion

        #region Initialization
        void SetupBuilder()
        {
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            int registeredProducts = 0;
            foreach (IAPItem product in IAPData.GetAllItems())
            {
                // The id getter returns empty outside Android / iOS, and an empty or "test" id would
                // either make the store fail to initialise or register an unusable product.
                if (string.IsNullOrEmpty(product.id) || product.id == "test")
                {
                    Debug.LogWarning($"<b><color=#FFA726>[IAP]</color></b> Skipping '{product.Name}' - its store product id is not set for this platform.");
                    continue;
                }

                builder.AddProduct(product.id, product.productType);
                registeredProducts++;
                Debug.Log("<b><color=#0F7EAE>Added Product ID:</color> </b>" + product.id);
            }

            if (registeredProducts == 0)
            {
                Debug.LogError("<b><color=red>[IAP]</color></b> No product has a store id for this platform - fill the Google Play / App Store ids in 'IAP Data'. Purchasing stays disabled.");
                return;
            }

            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            storeController = controller;
            storeExtensionProvider = extensions;
            isInitialized = true;
            onInitializationComplete?.Invoke();
            Debug.Log("<color=green><size=14><b>In App Purchasing Initialized Successfully!</b></size></color>");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.Log("<color=red><size=14><b>In App Purchasing Failed to Initialize! </b></size></color>" + error);
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.Log("<color=red><size=14><b>In App Purchasing Failed to Initialize! </b></size></color>" + error);
        }
       
        public void RestorePurchases()
        {
            if (!IsInitialized())
            {
                Debug.LogWarning("IAP Manager not initialized. Cannot proceed.");

            }
            // It's the first launch, restore purchases
            Debug.Log("Attempting to restore purchases...");
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                var appleExtensions = storeExtensionProvider.GetExtension<IAppleExtensions>();
                appleExtensions.RestoreTransactions(RestoreResult);
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                var googlePlayExtensions = storeExtensionProvider.GetExtension<IGooglePlayStoreExtensions>();
                googlePlayExtensions.RestoreTransactions(RestoreResult);
            }


        }
        
        void RestoreResult(bool success, string Result)
        {
            Debug.Log($"Restore Success= {success}: {Result}");
        }
        #endregion

        #region Purchasing
        public void PurchaseProduct(ProductName productName)
        {
            if (storeController == null)
            {
                Debug.LogWarning($"[IAP] Purchase of '{productName}' ignored - the store is not ready yet.");
                return;
            }

            IAPItem item = IAPData.GetItemByName(productName);
            if (item == null)
            {
                Debug.LogWarning($"[IAP] Purchase of '{productName}' ignored - no matching entry in IAP Data.");
                return;
            }

            if (string.IsNullOrEmpty(item.id))
            {
                Debug.LogWarning($"[IAP] Purchase of '{productName}' ignored - its store ID is empty.");
                return;
            }

            storeController.InitiatePurchase(item.id);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            var product = purchaseEvent.purchasedProduct;
            IAPItem item = IAPData.GetItemById(product.definition.id);
            if (item == null)
            {
                Debug.LogWarning($"[IAP] Purchase completed for unknown product id '{product.definition.id}'.");
                return PurchaseProcessingResult.Complete;
            }

            //Invoking Actions
            onPurchaseSuccess?.Invoke(item.Name);
            Debug.Log("purchase successful " + product.definition.id);

            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            IAPItem item = IAPData.GetItemById(product.definition.id);
            if (item != null) onPurchaseFail?.Invoke(item.Name);

            Debug.Log("purchase fail " + failureDescription.message);
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            IAPItem item = IAPData.GetItemById(product.definition.id);
            if (item != null) onPurchaseFail?.Invoke(item.Name);

            Debug.Log("purchase fail " + failureReason);
        }
        #endregion

        #region Checks
        public bool IsPurchased(ProductName name)
        {
            if (storeController == null)
            {
                Debug.Log("store controller not found");
                return false;
            }

            IAPItem item = IAPData.GetItemByName(name);
            if (item == null)
            {
                Debug.Log("this name Item not found");
                return false;
            }

            var product = storeController.products.WithID(item.id);
            if (product == null)
            {
                Debug.Log("Product is null");
                return false;
            }
            return product.hasReceipt;
        }
        public string GetPrice(ProductName name)
        {
            if (storeController == null)
            {
                return string.Empty;
            }

            IAPItem item = IAPData.GetItemByName(name);
            string id = item != null ? item.id : string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                Debug.Log("<color=red><size=14><b>Your Product Not Found! </b></size></color>");
                return "";
            }    
            Product product = storeController.products.WithID(id);
            if (product == null)
            {
                return string.Empty;
            }

            return product.metadata.localizedPriceString;
        }
        private bool IsInitialized()
        {
            return storeController != null && storeExtensionProvider != null;
        }

        #endregion
    }
}