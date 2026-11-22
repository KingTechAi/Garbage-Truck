using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace MangoMango.IAP
{
    
    [CreateAssetMenu(fileName = "IAP Data", menuName = "Mango Mango/IAP Data")]
    public class IAPData : ScriptableObject
    {
        [SerializeField] List<IAPItem> items;

        public IAPItem GetItemByName(ProductName productName)
        {
            IAPItem item = null;
            foreach (var i in items)
            {
                if (i.Name == productName)
                {
                    item = i;
                    break;
                }
            }

            return item;
        }
        public IAPItem GetItemById(string productId)
        {
            IAPItem item = null;
            foreach (var i in items)
            {
                if (i.id == productId)
                {
                    item = i;
                    break;
                }
            }

            return item;
        }
        public void RemoveDuplicteItems()
        {
            HashSet<IAPItem> hash = new HashSet<IAPItem>();
            foreach (var item in items)
            {
                hash.Add(item);
            }
            items.Clear();
            foreach (var item in hash)
            {
                items.Add(item);
            }
        }
        public List<IAPItem> GetAllItems()
        {
            return items;
        }
    }


    public enum ProductName
    {
        RemoveAds,
        BestBundle,
        Coins1,
        Coins2,
        Coins3,
        Coins4
    }
    [Serializable]
    public class IAPItem
    {
        public ProductName Name;
        public ProductType productType;

        public string googlePlayId;
        public string appStoreId;

        public string id
        {
            get
            {
#if UNITY_ANDROID
                return googlePlayId;
#elif UNITY_IOS
                return appStoreId;
#else
                return string.Empty;
#endif
            }
        }
    }
}
