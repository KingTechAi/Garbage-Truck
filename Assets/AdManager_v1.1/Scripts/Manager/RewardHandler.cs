using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewardHandler : MonoBehaviour
{
    public static RewardHandler instance;
   
   [HideInInspector] public bool isRewardAvailableCheck;
   
    public delegate void RewardedCompleteCheck();

   
    public static RewardedCompleteCheck OnRewardComplete;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != null)
        {
            Destroy(gameObject);
        }
    }

    // this is where you give reward
    
    public void GiveReward()
    {
        OnRewardComplete.Invoke();
    }

  
}
