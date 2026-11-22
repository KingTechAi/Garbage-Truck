using UnityEngine;

public class InternetChecker : MonoBehaviour
{
    public static InternetChecker instance;
    public GameObject noInternetPanel;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (noInternetPanel == null) return;

        // Reachability is driven in BOTH directions. The old code only reacted to the offline
        // case, so a panel that was active in the scene - or that got switched on during the
        // first frames, when reachability still reports NotReachable - stayed on screen forever,
        // even after the network came back. Hiding it again is what makes this "only when there
        // is no internet".
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;

        if (offline == noInternetPanel.activeSelf) return;

        noInternetPanel.SetActive(offline);
    }

    public void Retry()
    {
        if (noInternetPanel == null) return;

        // Update re-evaluates every frame, so Retry only needs to apply the current state
        // immediately instead of waiting for the next frame.
        noInternetPanel.SetActive(Application.internetReachability == NetworkReachability.NotReachable);
    }
}
