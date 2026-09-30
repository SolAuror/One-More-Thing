using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OneMoreThing
{
    [DefaultExecutionOrder(50)]
    public sealed class RoutineHUD : MonoBehaviour
    {
        public RoutineSession session;
        public PlayerInteractor interactor;
        public Text timerText, thoughtsText, promptText, feedbackText, planText;
        public Image progressFill;
        public GameObject progressRoot, modal;
        public Text modalTitle, modalBody;
        public Button primaryButton, restartButton, exportButton;
        public Text primaryLabel;
        [Min(1f)] public float thoughtSeconds = 7f;
        private readonly HashSet<string> shownThoughts = new();
        private RoutineState.TaskState currentThought;
        private float thoughtRemaining;
        private float thoughtGap;
        private RunPhase lastPhase = (RunPhase)(-1);
        private ScrollRect modalScroll;

        public void ConfigureCanvas()
        {
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1200, 900);
            // Preserve enough room on both axes: 4:3 and ultrawide must not crop menus.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        // Recover unassigned references from this HUD only, including inactive panels.
        // A missing legacy label must not prevent the morning button being wired up.
        public void ResolveReferences()
        {
            if (session == null) session = GetComponentInParent<RoutineSession>();
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
            Bind(ref timerText, "Clock/Countdown");
            Bind(ref thoughtsText, "Thought queue/Thoughts");
            Bind(ref promptText, "Interaction");
            Bind(ref feedbackText, "Feedback");
            Bind(ref planText, "Recall plan");
            Bind(ref progressFill, "Task progress/Fill");
            if (progressRoot == null) progressRoot = transform.Find("Task progress")?.gameObject;
            if (modal == null) modal = transform.Find("Morning and results")?.gameObject;
            Bind(ref modalTitle, "Morning and results/Title");
            Bind(ref modalBody, "Morning and results/Body");
            Bind(ref primaryButton, "Morning and results/Continue");
            Bind(ref restartButton, "Morning and results/Restart");
            Bind(ref exportButton, "Morning and results/Export");
            Bind(ref primaryLabel, "Morning and results/Continue/Label");
        }

        private void Bind<T>(ref T reference, string path) where T : Component
        {
            if (reference == null) reference = transform.Find(path)?.GetComponent<T>();
        }

        public bool HasRequiredReferences => session != null && thoughtsText != null && promptText != null
            && feedbackText != null && planText != null && progressFill != null && progressRoot != null
            && modal != null && modalTitle != null && modalBody != null && primaryButton != null
            && restartButton != null && exportButton != null && primaryLabel != null;

        private void Start()
        {
            ResolveReferences();
            if (!HasRequiredReferences)
            {
                Debug.LogError("The morning HUD is missing required UI objects. Restore its panels and button references.", this);
                enabled = false;
                return;
            }
            ConfigureCanvas();
            modalScroll = DeviceUI.MakeScrollable(modalBody);
            var carry = GetComponent<MorningCarryUI>();
            if (carry == null) carry = gameObject.AddComponent<MorningCarryUI>();
            carry.session = session;
            if (timerText != null) timerText.transform.parent.gameObject.SetActive(false);
            var oldThoughtPanel = thoughtsText.transform.parent;
            thoughtsText.transform.SetParent(transform, false);
            oldThoughtPanel.gameObject.SetActive(false);
            thoughtsText.rectTransform.anchorMin = thoughtsText.rectTransform.anchorMax = new Vector2(.5f, 0);
            thoughtsText.rectTransform.anchoredPosition = new Vector2(0, 265);
            thoughtsText.rectTransform.sizeDelta = new Vector2(720, 64);
            thoughtsText.fontSize = 22; thoughtsText.fontStyle = FontStyle.Italic; thoughtsText.alignment = TextAnchor.MiddleCenter;
            planText.rectTransform.anchorMin = planText.rectTransform.anchorMax = new Vector2(1, 0);
            planText.rectTransform.anchoredPosition = new Vector2(-180, 35);
            planText.rectTransform.sizeDelta = new Vector2(320, 40); planText.fontSize = 17;
            promptText.fontSize = 19;
            promptText.color = new Color(.9f, .93f, .9f, .88f);
            primaryButton.onClick.AddListener(() =>
            {
                if (session.State.Phase == RunPhase.Ready) session.State.Start();
                else if (session.State.Phase == RunPhase.Paused) session.State.SetPaused(false);
                else session.Restart();
            });
            restartButton.onClick.AddListener(session.Restart);
            exportButton.onClick.AddListener(() =>
            {
                try { session.ExportRun(); modalBody.text += "\n\nPlaytest log saved. The Console shows its location."; }
                catch (Exception error) { Debug.LogException(error); modalBody.text += "\nCould not save the log."; }
            });
        }

        private void LateUpdate()
        {
            if (session == null || session.State == null) return;
            var state = session.State;
            bool noteOpen = session.CanControl && Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            bool showThought = session.CanControl && !noteOpen && string.IsNullOrEmpty(session.Feedback);
            if (currentThought != null && currentThought.Completed) { currentThought = null; thoughtRemaining = 0f; }
            if (showThought)
            {
                thoughtGap = Mathf.Max(0f, thoughtGap - Time.unscaledDeltaTime);
                if (currentThought == null && thoughtGap <= 0f)
                {
                    currentThought = state.Thoughts.FirstOrDefault(t => !shownThoughts.Contains(t.Definition.id));
                    if (currentThought != null) { shownThoughts.Add(currentThought.Definition.id); thoughtRemaining = thoughtSeconds; }
                }
                if (currentThought != null)
                {
                    thoughtRemaining -= Time.unscaledDeltaTime;
                    if (thoughtRemaining <= 0f) { currentThought = null; thoughtGap = 2f; }
                }
            }
            thoughtsText.text = showThought ? currentThought?.Definition.thought ?? "" : "";
            promptText.text = (session.CanControl || state.Phase == RunPhase.Ready) && interactor != null ? interactor.Prompt : "";
            feedbackText.text = noteOpen ? "" : session.Feedback;
            bool phoneOpen = session.devices != null && session.devices.IsOpen && session.devices.IsPhone;
            feedbackText.rectTransform.anchorMin = feedbackText.rectTransform.anchorMax = new Vector2(.5f, 0);
            feedbackText.rectTransform.anchoredPosition = new Vector2(0, phoneOpen ? 50 : 185);
            feedbackText.rectTransform.sizeDelta = new Vector2(760, phoneOpen ? 85 : 65);
            feedbackText.alignment = TextAnchor.MiddleCenter;
            var crosshair = transform.Find("Crosshair");
            if (crosshair != null) crosshair.gameObject.SetActive(session.CanLook && !noteOpen);
            progressRoot.SetActive(session.CanControl && interactor != null && interactor.HasTask);
            progressFill.rectTransform.localScale = new Vector3(interactor != null ? interactor.Progress : 0f, 1f, 1f);
            planText.text = state.Phase == RunPhase.Ready ? DeviceUI.Clock(state) + "  ·  PHONE ALARM" : session.CanControl ? "Tab · note" + (session.HasPhone ? "     P · phone" : "") : "";
            modal.SetActive(state.Phase != RunPhase.Ready && !session.IsRunning);
            if (lastPhase == state.Phase) return;
            lastPhase = state.Phase;
            modalScroll.StopMovement();
            modalScroll.verticalNormalizedPosition = 1;
            restartButton.gameObject.SetActive(state.Phase == RunPhase.Paused);
            bool ended = state.Phase == RunPhase.Succeeded || state.Phase == RunPhase.TimedOut;
            exportButton.gameObject.SetActive(ended);
            primaryLabel.text = state.Phase == RunPhase.Ready ? "Start morning" : state.Phase == RunPhase.Paused ? "Resume" : "Try another morning";
            if (state.Phase == RunPhase.Ready)
            {
                modalTitle.text = "PHONE ALARM";
                modalBody.text = "07:55. The phone on the bedside table is ringing.\n\nE · silence alarm and get up";
            }
            else if (state.Phase == RunPhase.Paused)
            {
                modalTitle.text = "TAKE A BREATH";
                modalBody.text = "The timer is paused.\nYour unfinished tasks will be here when you return.";
            }
            else if (ended)
            {
                modalTitle.text = state.Phase == RunPhase.Succeeded ? "OUT THE DOOR" : "TIME TO GO...";
                modalBody.fontSize = 20;
                modalBody.text = string.Join("\n", state.Tasks.Where(t => t.Definition.showInSummary).Select(t =>
                    (t.EverCompleted ? "Done" + (t.CompletionCount > 1 ? " x" + t.CompletionCount : "") : t.Progress > 0f ? "Unfinished" : t.Discovered ? "Not started" : "Not noticed")
                    + "  -  " + t.Definition.title))
                    + "\n\n" + state.Switches + " task changes  /  " + state.Interruptions + " interruptions"
                    + "\nWhat pulled you away from what you meant to do?";
            }
        }
    }
}

