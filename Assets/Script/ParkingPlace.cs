using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParkingPlace : MonoBehaviour
{
    public float TimeToStay;
    public float timeLeft;
    public GameObject Truck;
    public GameObject ManMove;
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
            RccCanvas.SetActive(false);
            //Truck.GetComponent<Animator>().enabled = true;
            ManMove.SetActive(true);
            RccCam.gameObject.SetActive(false);
            Player.SetActive(false);
            OtherCam.SetActive(true);
            Truck.SetActive(true);
            gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        TimeStart = false;
        timeLeft = TimeToStay;
    }
}
