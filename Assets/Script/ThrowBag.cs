using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrowBag : MonoBehaviour
{
    public GameObject BagMan;
    public GameObject BagTruck;
    public GameObject AnimMan;
    public GameObject Truck;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ThrowBagNow()
    {
        BagMan.gameObject.SetActive(false);
        BagTruck.gameObject.SetActive(true);
        
        Invoke(nameof(GameEndLoad),0.2f);
    }
    public void GameEndLoad()
    {
        GameManger.Instance.ShowGameCompleteCanvus();
    }
    public void PlayBagThrow()
    {
        
        AnimMan.GetComponent<Animator>().SetTrigger("IsThrowing");
        
    }
}
