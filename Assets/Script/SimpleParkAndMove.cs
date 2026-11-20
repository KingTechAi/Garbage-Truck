using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleParkAndMove : MonoBehaviour
{
    public float TimeToStay;
    public float timeLeft;
    public GameObject Truck;
    
    public GameObject Player;
    public RCC_Camera RccCam;
    public GameObject RccCanvas;
    public GameObject OtherCam;
    bool TimeStart;
    // Start is called before the first frame update
    IEnumerator Start()
    {
        Time.timeScale = 1;
        yield return new WaitForSeconds(1f);
        Player = RccCam.playerCar.gameObject;
        timeLeft = TimeToStay;
        
    }

   

    void Update()
    {
        if (TimeStart == true)
        {
            timeLeft -= Time.deltaTime;
        }
    }


    private void OnTriggerStay(Collider other)
    {
        TimeStart = true;
        if (timeLeft < 0)
        {
            gameObject.GetComponent<BoxCollider>().enabled = false;
            Debug.Log("Time Stay Over Now Play Animation");
            TimeStart = false;
            timeLeft = TimeToStay;
            RccCam.gameObject.SetActive(false);
            RccCanvas.SetActive(false);
            Player.SetActive(false);
            OtherCam.SetActive(true);
            Truck.SetActive(true);
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            Invoke(nameof(MoveNow),0.3f);
        }
    }

    public void MoveNow()
    {
        print("Moveing called");
        Invoke(nameof(ShowGameOver), 1f);

    }
    public void ShowGameOver()
    {
        GameManger.Instance.ShowGameCompleteCanvus();
        print("game over called");
    }

    private void OnTriggerExit(Collider other)
    {
        TimeStart = false;
        timeLeft = TimeToStay;
    }
}
