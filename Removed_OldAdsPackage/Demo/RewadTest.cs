using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

public class RewadTest : MonoBehaviour
{
    private void OnEnable()
    {
        RewardHandler.OnRewardComplete  += GiveReward;
    }


    private void Start()
    {
        Debug.Log(RewardHandler.instance.isRewardAvailableCheck);
    }

    public void ShowInterstital()
    {
        if(AdsManager.instance)
            AdsManager.instance.Show_AdMob_Interstitial();
    }
    

    void GiveReward() // Actual function giving reward
    {
        Debug.Log("Ginving rewardsa ########################");
    }

 

    private void OnDisable()
    {
        RewardHandler.OnRewardComplete  -= GiveReward;
    }
}
