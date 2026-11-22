using UnityEngine;

namespace MangoMango.UI
{
    /// <summary>
    /// Minimal open/close helper so a button can drive a panel through a persistent onClick
    /// listener. Leave 'panel' empty to toggle the GameObject this component sits on.
    /// </summary>
    public class UIPanelToggle : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        [Tooltip("Close the panel on scene start, so it is only ever visible after Open() is called.")]
        [SerializeField] private bool startHidden = false;

        /// <summary>
        /// Resolved lazily and never null: a toggle on a panel that starts inactive has not run
        /// Awake yet when a button first calls Open(), so the field cannot be relied upon there.
        /// </summary>
        private GameObject Panel
        {
            get
            {
                if (panel == null) panel = gameObject;
                return panel;
            }
        }

        private void Awake()
        {
            if (startHidden && Panel.activeSelf) Panel.SetActive(false);
        }

        public void Open()
        {
            Panel.SetActive(true);
        }

        public void Close()
        {
            Panel.SetActive(false);
        }

        public void Toggle()
        {
            GameObject target = Panel;
            target.SetActive(!target.activeSelf);
        }
    }
}
