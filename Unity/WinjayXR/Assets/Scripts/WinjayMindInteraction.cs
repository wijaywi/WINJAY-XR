using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace WinjayXR
{
    /// <summary>Marks the rewind slider handle for grab-to-scrub.</summary>
    public sealed class MindSliderHandle : MonoBehaviour
    {
        public WinjayMindPanel Panel { get; set; }
    }

    /// <summary>
    /// Status panel for the mind map world: RUN/STEP/RESET, SAY presets
    /// (voice placeholder), the rewind scrubber and a LIVE jump-back button.
    /// </summary>
    public sealed class WinjayMindPanel : MonoBehaviour
    {
        public const float TrackMinX = -1.45f;
        public const float TrackMaxX = 0.75f;

        private WinjayMindController _controller;
        private TextMesh _title;
        private TextMesh _status;
        private TextMesh _settleNote;
        private TextMesh _stepNote;
        private TextMesh _viewLabel;
        private Transform _handle;
        private bool _dragging;
        private float _dragT;

        public bool IsDragging => _dragging;

        public static WinjayMindPanel Create(Transform parent, WinjayMindController controller)
        {
            // The console rides with the camera (screen-right, below the node
            // band) so it can never overlap the map: the nodes stay centered
            // while the panel holds a fixed slot in the view. In the headset
            // it floats in front of the wearer, as in the demo script.
            var cam = Camera.main;
            var anchor = cam != null ? cam.transform : parent;
            var go = new GameObject("MindPanel");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(1.7f, -0.7f, 4.0f);
            go.transform.localScale = Vector3.one * 0.5f;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(bg.GetComponent<Collider>());
            bg.name = "Bg";
            bg.transform.SetParent(go.transform, false);
            bg.transform.localScale = new Vector3(3.4f, 1.9f, 1f);
            // Opaque background: map text behind the console must not ghost
            // through a translucent quad.
            bg.GetComponent<MeshRenderer>().sharedMaterial =
                WinjayPrimitives.CreateUnlit(new Color(0.10f, 0.13f, 0.20f));

            var panel = go.AddComponent<WinjayMindPanel>();
            panel._controller = controller;

            panel._title = WinjayPrimitives.MakeLabel("WINJAY XR \u2014 LIVING MIND MAP", WinjayPalette.MindExplorer, 0.115f, 54);
            panel._title.transform.SetParent(go.transform, false);
            panel._title.transform.localPosition = new Vector3(0f, 0.80f, -0.01f);
            WinjayPrimitives.FitLabel(panel._title, 3.24f, 0.18f, false);

            panel._status = WinjayPrimitives.MakeLabel("", new Color(1f, 0.83f, 0.3f), 0.082f, 46);
            panel._status.transform.SetParent(go.transform, false);
            panel._status.transform.localPosition = new Vector3(0f, 0.60f, -0.01f);
            WinjayPrimitives.FitLabel(panel._status, 3.24f, 0.12f, false);

            panel._stepNote = WinjayPrimitives.MakeLabel("", Color.white, 0.072f, 42);
            panel._stepNote.transform.SetParent(go.transform, false);
            panel._stepNote.transform.localPosition = new Vector3(0f, 0.42f, -0.01f);
            WinjayPrimitives.FitLabel(panel._stepNote, 3.24f, 0.11f, false);

            panel._settleNote = WinjayPrimitives.MakeLabel("", new Color(1f, 0.83f, 0.3f), 0.052f, 36);
            panel._settleNote.transform.SetParent(go.transform, false);
            panel._settleNote.transform.localPosition = new Vector3(0f, 0.26f, -0.01f);
            WinjayPrimitives.FitLabel(panel._settleNote, 3.24f, 0.08f, false);

            var rewindCaption = WinjayPrimitives.MakeLabel("REWIND", new Color(0.55f, 0.62f, 0.72f), 0.062f, 40);
            rewindCaption.transform.SetParent(go.transform, false);
            rewindCaption.transform.localPosition = new Vector3(-1.55f, 0.17f, -0.01f);
            WinjayPrimitives.FitLabel(rewindCaption, 0.88f, 0.075f, false);

            var track = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(track.GetComponent<Collider>());
            track.name = "Track";
            track.transform.SetParent(go.transform, false);
            track.transform.localPosition = new Vector3(-0.35f, 0.17f, 0f);
            track.transform.localScale = new Vector3(TrackMaxX - TrackMinX + 0.2f, 0.05f, 1f);
            track.GetComponent<MeshRenderer>().sharedMaterial = WinjayPrimitives.CreateUnlit(new Color(0.16f, 0.21f, 0.30f));

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(go.transform, false);
            handle.transform.localPosition = new Vector3(TrackMaxX, 0.17f, -0.02f);
            handle.transform.localScale = new Vector3(0.115f, 0.115f, 0.09f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = WinjayPalette.CreateLit(WinjayPalette.MindLock, 0.6f);
            var handleComp = handle.AddComponent<MindSliderHandle>();
            handleComp.Panel = panel;
            panel._handle = handle.transform;

            WinjayButton.Create(go.transform, new Vector3(1.30f, 0.17f, 0f), "LIVE", WinjayPalette.MindEdgeBridge,
                () => panel.JumpLive(), 0.44f);

            panel._viewLabel = WinjayPrimitives.MakeLabel("", new Color(0.62f, 0.70f, 0.82f), 0.062f, 40);
            panel._viewLabel.transform.SetParent(go.transform, false);
            panel._viewLabel.transform.localPosition = new Vector3(-0.35f, -0.02f, -0.01f);
            WinjayPrimitives.FitLabel(panel._viewLabel, 3.2f, 0.085f, false);

            var y = -0.34f;
            WinjayButton.Create(go.transform, new Vector3(-1.02f, y, 0f), "RUN", WinjayPalette.States["Supported"], () =>
            {
                if (controller.IsRunning) controller.StopRun();
                else controller.Run();
            }, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(-0.34f, y, 0f), "STEP", WinjayPalette.States["Investigating"], controller.Step, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(0.34f, y, 0f), "RESET", WinjayPalette.States["Created"], controller.Reset, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(1.02f, y, 0f), "MACHINE", WinjayPalette.States["Falsifying"],
                () => WinjayBootstrap.Switch(false), 0.56f);

            var sayCaption = WinjayPrimitives.MakeLabel("SAY \u2014 voice placeholder (voice lives on the headset mic)",
                new Color(0.55f, 0.62f, 0.72f), 0.058f, 38);
            sayCaption.transform.SetParent(go.transform, false);
            sayCaption.transform.localPosition = new Vector3(0f, -0.545f, -0.01f);
            WinjayPrimitives.FitLabel(sayCaption, 3.24f, 0.075f, false);

            var sayY = -0.70f;
            WinjayButton.Create(go.transform, new Vector3(-1.12f, sayY, 0f), "I easily get motion sick",
                WinjayPalette.MindHuman, () => controller.Say("I easily get motion sick"), 1.06f);
            WinjayButton.Create(go.transform, new Vector3(0f, sayY, 0f), "Slow mornings, please",
                WinjayPalette.MindHuman, () => controller.Say("Slow mornings, please"), 1.06f);
            WinjayButton.Create(go.transform, new Vector3(1.12f, sayY, 0f), "Rent a scooter",
                WinjayPalette.MindHuman, () => controller.Say("Rent a scooter"), 1.06f);

            var billboard = go.AddComponent<Billboard>();
            billboard.yawOnly = true;
            billboard.tiltDegrees = 38f;
            return panel;
        }

        private void OnEnable()
        {
            if (_controller != null) _controller.StoryAdvanced += OnStoryAdvanced;
        }

        private void OnDisable()
        {
            if (_controller != null) _controller.StoryAdvanced -= OnStoryAdvanced;
        }

        private void OnStoryAdvanced(Winjay.Agents.Mind.MindStoryStep step)
        {
            if (step == null) return;
            _stepNote.text = step.Actor + " \u00B7 " + step.Note;
            _stepNote.color = ActorColor(step.Actor);
        }

        private static Color ActorColor(string actor)
        {
            switch (actor)
            {
                case "Connector": return WinjayPalette.MindConnector;
                case "Challenger": return WinjayPalette.MindChallenger;
                case "Human": return WinjayPalette.MindHuman;
                default: return WinjayPalette.MindExplorer;
            }
        }

        private void Update()
        {
            if (_controller == null) return;
            var running = _controller.IsRunning;
            var settled = _controller.Phase == WinjayMindController.StoryPhase.Settled;
            _status.text = "tick " + _controller.TickCount + "  \u00B7  " + _controller.EventCount + " events  \u00B7  " +
                           (running ? "running\u2026" : settled ? "settled" : "ready");
            _settleNote.text = settled && _controller.IsLive ? _controller.StatusLine : "";

            if (!_dragging) UpdateHandleFromView();
            _viewLabel.text = _controller.IsLive
                ? "live \u2014 " + _controller.EventCount + " events"
                : "viewing #" + _controller.ViewUpto + " / " + _controller.EventCount + "  (pull the handle back to live)";
        }

        private void UpdateHandleFromView()
        {
            var total = Mathf.Max(1, _controller.EventCount);
            var t = _controller.ViewUpto / (float)total;
            _handle.localPosition = new Vector3(Mathf.Lerp(TrackMinX, TrackMaxX, t), 0.17f, -0.02f);
        }

        // ------------------------------------------------------- scrub drag

        public void BeginDrag()
        {
            _dragging = true;
            _dragT = 1f;
        }

        public void DragTo(Ray ray)
        {
            var plane = new Plane(transform.forward, transform.position);
            float enter;
            if (!plane.Raycast(ray, out enter)) return;
            var world = ray.GetPoint(enter);
            var local = transform.InverseTransformPoint(world);
            var x = Mathf.Clamp(local.x, TrackMinX, TrackMaxX);
            _dragT = Mathf.InverseLerp(TrackMinX, TrackMaxX, x);
            _handle.localPosition = new Vector3(x, 0.17f, -0.02f);

            var upto = Mathf.RoundToInt(_dragT * _controller.EventCount);
            _viewLabel.text = _dragT >= 0.995f
                ? "live \u2014 " + _controller.EventCount + " events"
                : "viewing #" + upto + " / " + _controller.EventCount;
        }

        public void EndDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            if (_dragT >= 0.995f) _controller.GoLive();
            else _controller.Rewind(Mathf.RoundToInt(_dragT * _controller.EventCount));
            UpdateHandleFromView();
        }

        public void JumpLive()
        {
            _controller.GoLive();
            UpdateHandleFromView();
        }
    }

    /// <summary>Floating card for a selected mind node, with curator actions.</summary>
    public sealed class WinjayMindCard : MonoBehaviour
    {
        private static readonly Vector2 Size = new Vector2(2.15f, 1.35f);

        private WinjayMindController _controller;
        private TextMesh _title;
        private TextMesh _meta;
        private TextMesh _why;
        private string _nodeId;

        public static WinjayMindCard Create(Transform parent, WinjayMindController controller)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.name = "MindCard";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(Size.x, Size.y, 1f);
            // Opaque so map text behind the card cannot ghost through it.
            go.GetComponent<MeshRenderer>().sharedMaterial =
                WinjayPrimitives.CreateUnlit(new Color(0.05f, 0.07f, 0.12f));

            var card = go.AddComponent<WinjayMindCard>();
            card._controller = controller;

            card._title = WinjayPrimitives.MakeLabel("", Color.white, 0.085f, 46);
            card._title.transform.SetParent(go.transform, false);
            card._title.transform.localPosition = new Vector3(0f, 0.44f, -0.01f);
            WinjayPrimitives.FitLabel(card._title, 1.92f, 0.17f);

            card._meta = WinjayPrimitives.MakeLabel("", new Color(0.72f, 0.79f, 0.89f), 0.062f, 40);
            card._meta.transform.SetParent(go.transform, false);
            card._meta.transform.localPosition = new Vector3(0f, 0.26f, -0.01f);
            WinjayPrimitives.FitLabel(card._meta, 1.92f, 0.12f);

            card._why = WinjayPrimitives.MakeLabel("", new Color(0.62f, 0.70f, 0.82f), 0.058f, 38);
            card._why.transform.SetParent(go.transform, false);
            card._why.transform.localPosition = new Vector3(0f, 0.12f, -0.01f);
            WinjayPrimitives.FitLabel(card._why, 1.92f, 0.10f);

            go.AddComponent<Billboard>();
            go.SetActive(false);
            return card;
        }

        public string NodeId => _nodeId;

        public void Show(string nodeId, Vector3 worldPos)
        {
            _nodeId = nodeId;
            Refresh();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var cam = Camera.main;
            var lift = new Vector3(0f, 0.42f, 0f);
            transform.position = cam != null
                ? worldPos + (cam.transform.position - worldPos).normalized * 0.85f + lift
                : worldPos + lift;
        }

        public void Hide()
        {
            _nodeId = null;
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            var engine = _controller != null ? _controller.Engine : null;
            var node = engine != null && _nodeId != null ? engine.GetNode(_nodeId) : null;
            if (node == null) { Hide(); return; }

            _title.text = "\u201C" + node.Label + "\u201D";
            _meta.text = "made by " + node.Contributor + "  \u00B7  strength " + node.Strength.ToString("0.00") +
                         (node.Phase != null ? "  \u00B7  " + node.Phase : "") +
                         (node.Locked ? "  \u00B7  LOCKED (gold, frozen)" : "") +
                         (node.Ghost ? "  \u00B7  ghost suggestion" : "");
            _why.text = node.Ghost
                ? "accept to bring it into the map \u2014 its strength starts low and must earn support"
                : KeepPinchHint();

            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Button_")) Destroy(child.gameObject);
            }

            var slots = new System.Collections.Generic.List<WinjayButton>();
            if (node.Ghost)
            {
                slots.Add(WinjayButton.Create(transform, new Vector3(-0.5f, -0.45f, 0f), "ACCEPT",
                    WinjayPalette.MindConnector, () => Act(() => _controller.AcceptGhost(_nodeId)), 0.9f));
                slots.Add(WinjayButton.Create(transform, new Vector3(0.5f, -0.45f, 0f), "REJECT",
                    WinjayPalette.MindEdgeConflict, () => Act(() => _controller.RejectGhost(_nodeId)), 0.9f));
            }
            else
            {
                slots.Add(WinjayButton.Create(transform, new Vector3(-0.72f, -0.45f, 0f),
                    node.Locked ? "UNLOCK" : "LOCK (GOLD)", WinjayPalette.MindLock,
                    () => Act(() => _controller.ToggleLock(_nodeId)), 0.66f));
                if (node.Kind != Winjay.Core.Mind.MindNodeKind.Root)
                {
                    slots.Add(WinjayButton.Create(transform, new Vector3(0f, -0.45f, 0f), "PRUNE",
                        WinjayPalette.MindEdgeConflict, () => Act(() => _controller.Prune(_nodeId)), 0.66f));
                }
                slots.Add(WinjayButton.Create(transform, new Vector3(0.72f, -0.45f, 0f), "CLOSE",
                    WinjayPalette.States["Created"], Hide, 0.66f));
            }
        }

        private static string KeepPinchHint()
        {
            return "click / pinch a node to inspect  \u00B7  keep pinching 1.5 s to lock it gold";
        }

        private void Act(Action action)
        {
            action();
            var engine = _controller != null ? _controller.Engine : null;
            var node = engine != null && _nodeId != null ? engine.GetNode(_nodeId) : null;
            if (node == null || node.Pruned) Hide();
            else Refresh();
        }

        private void Update()
        {
            if (_nodeId == null) return;
            var engine = _controller != null ? _controller.Engine : null;
            var node = engine != null ? engine.GetNode(_nodeId) : null;
            if (node == null || node.Pruned) Hide();
        }
    }

    /// <summary>
    /// Curator interaction for the mind map: pinch to point and select,
    /// buttons for curator actions, grab the handle to rewind. Falls back to
    /// mouse on desktop. Long pinch on a node locks it (gold).
    /// </summary>
    public sealed class WinjayMindInteraction : MonoBehaviour
    {
        private const float LongPinchSeconds = 1.5f;

        public WinjayMindController Controller { get; set; }

        private XrEnvironmentSetup _env;
        private WinjayMindPanel _panel;
        private WinjayMindCard _card;
        private LineRenderer _laser;
        private GameObject _hitRing;
        private Camera _cam;

        private bool _leftWasPinching;
        private bool _rightWasPinching;
        private WinjayButton _hoveredButton;
        private string _hoveredNodeId;

        private bool _draggingHandle;
        private string _pinchNodeId;
        private float _pinchStartTime;
        private bool _lockFired;

        private void Start()
        {
            _env = GetComponent<XrEnvironmentSetup>();
            _panel = WinjayMindPanel.Create(transform, Controller);
            _card = WinjayMindCard.Create(transform, Controller);

            var laserGo = new GameObject("MindLaser");
            laserGo.transform.SetParent(transform, false);
            _laser = laserGo.AddComponent<LineRenderer>();
            _laser.material = WinjayPrimitives.CreateUnlit(WinjayPalette.MindExplorer);
            _laser.startWidth = 0.006f;
            _laser.endWidth = 0.006f;
            _laser.positionCount = 2;
            _laser.useWorldSpace = true;
            _laser.enabled = false;

            _hitRing = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(_hitRing.GetComponent<Collider>());
            _hitRing.transform.SetParent(transform, false);
            _hitRing.transform.localScale = Vector3.one * 0.06f;
            _hitRing.GetComponent<MeshRenderer>().sharedMaterial =
                WinjayPrimitives.CreateUnlit(WinjayPalette.MindExplorer);
            _hitRing.SetActive(false);
            _hitRing.AddComponent<Billboard>();
        }

        private void Update()
        {
            _cam = _cam != null ? _cam : Camera.main;
            if (_cam == null) return;

            if (WinjayPrimitives.XrDisplayActive()) UpdateHands();
            else UpdateMouse();
        }

        // ------------------------------------------------------------- mouse

        private void UpdateMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            var hit = Cast(ray);
            SetHover(hit.Button, hit.Tag);
            ShowLaser(ray, hit);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (hit.Button != null) hit.Button.Press();
                else if (hit.Handle != null && !_draggingHandle) { _draggingHandle = true; _panel.BeginDrag(); }
                else if (hit.Tag != null) Select(hit.Tag);
                else Deselect();
                return;
            }

            if (_draggingHandle)
            {
                if (mouse.leftButton.isPressed) _panel.DragTo(ray);
                else { _draggingHandle = false; _panel.EndDrag(); }
            }
        }

        // ------------------------------------------------------------- hands

        private void UpdateHands()
        {
            foreach (var handType in new[] { OVRHand.Hand.HandLeft, OVRHand.Hand.HandRight })
            {
                var hand = _env != null ? _env.GetHand(handType) : null;
                if (hand == null || !hand.IsTracked) continue;

                var pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                var wasPinching = handType == OVRHand.Hand.HandLeft ? _leftWasPinching : _rightWasPinching;
                var pose = hand.PointerPose;
                var ray = new Ray(pose.position, pose.forward);

                if (pinching)
                {
                    var hit = Cast(ray);
                    ShowLaser(ray, hit);
                    SetHover(hit.Button, hit.Tag);

                    if (!wasPinching)
                    {
                        if (hit.Button != null) hit.Button.Press();
                        else if (hit.Handle != null) { _draggingHandle = true; _panel.BeginDrag(); }
                        else if (hit.Tag != null)
                        {
                            Select(hit.Tag);
                            _pinchNodeId = hit.Tag.Id;
                            _pinchStartTime = Time.time;
                            _lockFired = false;
                        }
                        else Deselect();
                    }
                    else if (_draggingHandle)
                    {
                        _panel.DragTo(ray);
                    }
                    else if (_pinchNodeId != null && !_lockFired &&
                             Time.time - _pinchStartTime >= LongPinchSeconds)
                    {
                        Controller.ToggleLock(_pinchNodeId);
                        _lockFired = true;
                        if (_card != null && _card.NodeId == _pinchNodeId)
                            _card.Show(_pinchNodeId, Controller.View.WorldPos(_pinchNodeId));
                    }
                }
                else
                {
                    if (wasPinching)
                    {
                        if (_draggingHandle) { _draggingHandle = false; _panel.EndDrag(); }
                        _pinchNodeId = null;
                        _lockFired = false;
                    }
                }

                if (handType == OVRHand.Hand.HandLeft) _leftWasPinching = pinching;
                else _rightWasPinching = pinching;
            }
        }

        // ------------------------------------------------------------- shared

        private struct Hit
        {
            public WinjayButton Button;
            public MindSliderHandle Handle;
            public MindNodeTag Tag;
            public Vector3 Point;
        }

        private Hit Cast(Ray ray)
        {
            var hit = new Hit();
            RaycastHit info;
            if (Physics.Raycast(ray, out info, 25f))
            {
                hit.Point = info.point;
                hit.Button = info.collider.GetComponentInParent<WinjayButton>();
                hit.Handle = info.collider.GetComponentInParent<MindSliderHandle>();
                hit.Tag = info.collider.GetComponentInParent<MindNodeTag>();
            }
            return hit;
        }

        private void SetHover(WinjayButton button, MindNodeTag tag)
        {
            if (_hoveredButton != button)
            {
                if (_hoveredButton != null) _hoveredButton.SetHover(false);
                _hoveredButton = button;
                if (_hoveredButton != null) _hoveredButton.SetHover(true);
            }

            var nodeId = tag != null ? tag.Id : null;
            if (_hoveredNodeId != nodeId)
            {
                _hoveredNodeId = nodeId;
                if (Controller != null && Controller.View != null) Controller.View.SetHover(nodeId);
            }
        }

        private void ShowLaser(Ray ray, Hit hit)
        {
            _laser.enabled = true;
            _laser.SetPosition(0, ray.origin);
            var end = hit.Point != Vector3.zero ? hit.Point : ray.origin + ray.direction * 12f;
            _laser.SetPosition(1, end);
            _hitRing.SetActive(hit.Tag != null || hit.Button != null || hit.Handle != null);
            if (_hitRing.activeSelf)
            {
                _hitRing.transform.position = end;
                _hitRing.transform.localScale = Vector3.one * (hit.Tag != null ? 0.12f : 0.06f);
            }
        }

        private void Select(MindNodeTag tag)
        {
            if (Controller == null || Controller.View == null) return;
            Controller.View.SetSelected(tag.Id);
            if (_card.NodeId == tag.Id) { Deselect(); return; }
            _card.Show(tag.Id, Controller.View.WorldPos(tag.Id));
        }

        private void Deselect()
        {
            if (Controller != null && Controller.View != null) Controller.View.SetSelected(null);
            if (_card != null) _card.Hide();
        }
    }
}
