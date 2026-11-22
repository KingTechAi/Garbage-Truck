using TMPro;
using UnityEngine;

namespace MangoMango.IAP
{
    /// <summary>
    /// Keeps one money label in sync with CoinManager.Balance. Drop it on any money Text (MainMenu,
    /// MachineSelection, InApps, gameplay HUD...): it repaints on enable and on every wallet change.
    /// </summary>
    public class CoinLabel : MonoBehaviour
    {
        [SerializeField] private string prefix = "$ ";
        [SerializeField] private TMP_Text target;

        private void Awake()
        {
            if (target == null) target = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            CoinManager.OnCoinsChanged += HandleCoinsChanged;
            Refresh();
        }

        private void OnDisable()
        {
            CoinManager.OnCoinsChanged -= HandleCoinsChanged;
        }

        private void HandleCoinsChanged(int balance) => Refresh();

        private void Refresh()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (target != null) target.text = prefix + CoinManager.Balance;
        }
    }
}
