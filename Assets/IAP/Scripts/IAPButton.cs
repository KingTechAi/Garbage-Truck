using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MangoMango.IAP
{
    [RequireComponent(typeof(Button))]
    public class IAPButton : MonoBehaviour
    {
        Button button;

        public ProductName productName;
        public TextMeshProUGUI priceText;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonClick);
        }

        private void Start()
        {
            if (IAPManager.Instance == null)
            {
                Debug.LogWarning("[IAP] IAP Manager not found - the purchase buttons need the IAP Manager prefab in the Splash scene.");
                return;
            }

            // The store is initialised asynchronously, so the price is written on Start *and* again
            // once the store answers with the localized price strings.
            IAPManager.Instance.onInitializationComplete += RefreshPrice;
            RefreshPrice();
        }

        private void OnDestroy()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.onInitializationComplete -= RefreshPrice;
            }
        }

        private void RefreshPrice()
        {
            if (priceText == null || IAPManager.Instance == null || !IAPManager.Instance.isInitialized) return;

            string localizedPrice = IAPManager.Instance.GetPrice(productName);

            // Keep the designed price on screen while the store cannot resolve the product
            // (Editor / no store connection / placeholder product id).
            if (!string.IsNullOrEmpty(localizedPrice)) priceText.text = localizedPrice;
        }

        private void OnButtonClick()
        {
            if (IAPManager.Instance == null)
            {
                Debug.LogWarning("[IAP] Purchase ignored - IAP Manager is not in the scene.");
                return;
            }

            IAPManager.Instance.PurchaseProduct(productName);
        }
    }
}
