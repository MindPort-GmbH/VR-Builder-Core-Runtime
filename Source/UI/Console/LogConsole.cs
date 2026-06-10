using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VRBuilder.Core.Utils;

namespace VRBuilder.UI.Console
{
    /// <summary>
    /// Console implementation for an in-world UI using UI Toolkit rendered in world space.
    /// </summary>
    public class LogConsole : MonoBehaviour, ILogConsole
    {
        [SerializeField]
        private VisualTreeAsset logItemTemplate = null;

        [SerializeField]
        private UIDocument document = null;

        [SerializeField]
        private VRBConsolePlacer placer = null;

        [SerializeField]
        [Tooltip("Objects toggled when the console is shown or hidden. The root object stays active so queued messages keep being processed.")]
        private GameObject[] visuals = new GameObject[0];

        private List<LogMessage> logs = new List<LogMessage>();
        private ListView listView;
        private bool isDirty = false;

        /// <summary>
        /// True if the console window is currently visible.
        /// </summary>
        public bool IsVisible => visuals.Length > 0 && visuals[0].activeSelf;

        private void Awake()
        {
            if (document == null)
            {
                document = GetComponentInChildren<UIDocument>(true);
            }

            if (placer == null)
            {
                placer = GetComponent<VRBConsolePlacer>();
            }
        }

        private void Update()
        {
            if (isDirty)
            {
                isDirty = false;
                VRBConsole.Refresh();
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            logs.Clear();
            RefreshList();
        }

        /// <inheritdoc/>
        public void Show()
        {
            if (IsVisible == false)
            {
                placer?.PlaceInFrontOfUser();
            }

            foreach (GameObject visual in visuals)
            {
                visual.SetActive(true);
            }

            BindDocument();
            RefreshList();
        }

        /// <inheritdoc/>
        public void Hide()
        {
            foreach (GameObject visual in visuals)
            {
                visual.SetActive(false);
            }

            listView = null;
        }

        /// <inheritdoc/>
        public void Toggle()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <inheritdoc/>
        public void LogMessage(string message, string details, LogType logType)
        {
            logs.Add(new LogMessage(message, details, logType));
            RefreshList();
        }

        /// <inheritdoc/>
        public void SetDirty()
        {
            isDirty = true;
        }

        private void BindDocument()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;

            if (root == null)
            {
                return;
            }

            listView = root.Q<ListView>("LogList");

            if (listView != null)
            {
                listView.makeItem = () => logItemTemplate.CloneTree();
                listView.bindItem = BindItem;
                listView.itemsSource = logs;
            }

            Button closeButton = root.Q<Button>("CloseButton");

            if (closeButton != null)
            {
                closeButton.clicked -= Hide;
                closeButton.clicked += Hide;
            }

            Button clearButton = root.Q<Button>("ClearButton");

            if (clearButton != null)
            {
                clearButton.clicked -= Clear;
                clearButton.clicked += Clear;
            }
        }

        private void BindItem(VisualElement element, int index)
        {
            LogMessage log = logs[index];

            Label message = element.Q<Label>("Message");

            if (message != null)
            {
                message.text = log.Message;
            }

            Label icon = element.Q<Label>("Icon");

            if (icon != null)
            {
                bool isError = log.LogType == LogType.Error || log.LogType == LogType.Exception || log.LogType == LogType.Assert;
                icon.EnableInClassList("console-icon--log", log.LogType == LogType.Log);
                icon.EnableInClassList("console-icon--warning", log.LogType == LogType.Warning);
                icon.EnableInClassList("console-icon--error", isError);
                icon.text = log.LogType == LogType.Warning ? "!" : log.LogType == LogType.Log ? "i" : "✕";
            }
        }

        private void RefreshList()
        {
            if (IsVisible == false || listView == null)
            {
                return;
            }

            listView.RefreshItems();

            if (logs.Count > 0)
            {
                listView.ScrollToItem(-1);
            }
        }
    }
}
