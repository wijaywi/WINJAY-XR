using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace WinjayXR
{
    /// <summary>Marks a spatial entity so raycasts can identify it.</summary>
    public sealed class WinjayEntityTag : MonoBehaviour
    {
        public string Id { get; set; }
    }

    /// <summary>
    /// World-space pressable button (quad + label + collider).
    /// Works with hand pinch and mouse click.
    /// </summary>
    public sealed class WinjayButton : MonoBehaviour
    {
        public event Action Pressed;

        private TextMesh _label;
        private Material _mat;
        private Color _base;
        private bool _hover;

        public static WinjayButton Create(Transform parent, Vector3 localPos, string text, Color color, Action onPress, float width = 0.42f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.name = "Button_" + text;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(width, 0.16f, 1f);
            var mat = WinjayPrimitives.CreateUnlit(color);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            // LabelSlot cancels the button's non-uniform scale so the text keeps
            // its shape while still shrinking with any scaled parent panel.
            var slot = new GameObject("LabelSlot");
            slot.transform.SetParent(go.transform, false);
            slot.transform.localScale = new Vector3(1f / width, 1f / 0.16f, 1f);
            var label = WinjayPrimitives.MakeLabel(text, Color.white, 0.09f, 52);
            label.transform.SetParent(slot.transform, false);
            label.transform.localPosition = new Vector3(0, 0, -0.01f);
            WinjayPrimitives.FitLabel(label, width * 0.86f, 0.085f, false);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(width, 0.16f, 0.02f);

            var button = go.AddComponent<WinjayButton>();
            button._label = label;
            button._mat = mat;
            button._base = color;
            if (onPress != null) button.Pressed += onPress;
            return button;
        }

        public void SetText(string text)
        {
            _label.text = text;
        }

        public void SetHover(bool hover)
        {
            _hover = hover;
            _mat.color = hover ? Color.Lerp(_base, Color.white, 0.45f) : _base;
            _label.color = hover ? Color.black : Color.white;
        }

        public void Press()
        {
            Pressed?.Invoke();
            SetHover(false);
        }
    }

    /// <summary>Floating card that explains a selected entity (state, not chat).</summary>
    public sealed class WinjayInfoCard : MonoBehaviour
    {
        private TextMesh _text;
        private MeshRenderer _bg;
        private WinjayEntityTag _target;
        private readonly Vector2 _size = new Vector2(1.9f, 1.15f);

        public static WinjayInfoCard Create(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.name = "InfoCard";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(1.9f, 1.15f, 1f);
            var mat = WinjayPrimitives.CreateUnlit(WinjayPalette.PanelBg);
            mat.color = new Color(0.05f, 0.07f, 0.12f, 0.92f);
            var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader != null) mat = new Material(shader);
            if (shader != null) mat.color = new Color(0.06f, 0.08f, 0.13f, 0.93f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var card = go.AddComponent<WinjayInfoCard>();
            card._bg = go.GetComponent<MeshRenderer>();
            var tm = WinjayPrimitives.MakeLabel("", Color.white, 0.075f, 44);
            tm.transform.SetParent(go.transform, false);
            tm.transform.localPosition = new Vector3(0, 0.12f, -0.01f);
            tm.anchor = TextAnchor.UpperCenter;
            tm.alignment = TextAlignment.Center;
            card._text = tm;
            WinjayPrimitives.FitLabel(tm, 1.72f, 0.95f);
            go.SetActive(false);
            go.AddComponent<Billboard>();
            return card;
        }

        public void Show(WinjayEntityTag target, string summary, Vector3 worldPos)
        {
            _target = target;
            _text.text = summary ?? string.Empty;
            gameObject.SetActive(true);
            transform.position = worldPos + Vector3.up * 0.75f;
            var cam = Camera.main;
            if (cam != null)
            {
                var dir = (transform.position - cam.transform.position).normalized;
                transform.position = worldPos + dir * 0.55f + Vector3.up * 0.45f;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _target = null;
        }

        public WinjayEntityTag Target => _target;
    }

    /// <summary>Status panel with RUN/STEP/RESET/SCENARIO buttons.</summary>
    public sealed class WinjayStatusPanel : MonoBehaviour
    {
        private TextMesh _title;
        private TextMesh _tickLine;
        private TextMesh _verdict;
        private WinjaySceneController _controller;

        public static WinjayStatusPanel Create(Transform parent, WinjaySceneController controller)
        {
            var go = new GameObject("StatusPanel");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 1.5f, -3.2f);
            go.transform.rotation = Quaternion.identity;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(bg.GetComponent<Collider>());
            bg.name = "Bg";
            bg.transform.SetParent(go.transform, false);
            bg.transform.localScale = new Vector3(3.4f, 1.15f, 1f);
            var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            var mat = shader != null
                ? new Material(shader)
                : WinjayPrimitives.CreateUnlit(WinjayPalette.PanelBg);
            if (shader != null) mat.color = new Color(0.06f, 0.08f, 0.13f, 0.9f);
            bg.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var panel = go.AddComponent<WinjayStatusPanel>();
            panel._controller = controller;

            panel._title = WinjayPrimitives.MakeLabel("WINJAY XR", new Color(0.56f, 0.82f, 1f), 0.14f, 56);
            panel._title.transform.SetParent(go.transform, false);
            panel._title.transform.localPosition = new Vector3(0, 0.42f, -0.01f);
            WinjayPrimitives.FitLabel(panel._title, 3.2f, 0.24f);

            panel._tickLine = WinjayPrimitives.MakeLabel("", new Color(1f, 0.83f, 0.3f), 0.09f);
            panel._tickLine.transform.SetParent(go.transform, false);
            panel._tickLine.transform.localPosition = new Vector3(-0.95f, 0.12f, -0.01f);
            WinjayPrimitives.FitLabel(panel._tickLine, 2.9f, 0.14f);

            panel._verdict = WinjayPrimitives.MakeLabel("", Color.white, 0.085f);
            panel._verdict.transform.SetParent(go.transform, false);
            panel._verdict.transform.localPosition = new Vector3(0, -0.16f, -0.01f);
            WinjayPrimitives.FitLabel(panel._verdict, 3.2f, 0.22f);

            var y = -0.42f;
            WinjayButton.Create(go.transform, new Vector3(-1.36f, y, 0), "RUN", WinjayPalette.States["Supported"], () =>
            {
                if (controller.IsRunning) controller.Stop();
                else controller.Run();
            }, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(-0.68f, y, 0), "STEP", WinjayPalette.States["Investigating"], controller.Step, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(0f, y, 0), "RESET", WinjayPalette.States["Created"], controller.Reset, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(0.68f, y, 0), "SCENARIO", WinjayPalette.States["Falsifying"], controller.ToggleScenario, 0.56f);
            WinjayButton.Create(go.transform, new Vector3(1.36f, y, 0), "MIND MAP", WinjayPalette.MindEdgeBridge, () => WinjayBootstrap.Switch(true), 0.56f);

            go.AddComponent<Billboard>();
            return panel;
        }

        private void Update()
        {
            if (_controller == null) return;
            var run = _controller.IsRunning;
            _title.text = "WINJAY XR — " + _controller.ScenarioTitle;
            _tickLine.text = "tick " + _controller.TickCount + (run ? "  •  running…" : "  •  idle");
            var decision = _controller.Engine != null ? _controller.Engine.Get<Winjay.Core.Model.Decision>("DC001") : null;
            _verdict.text = decision != null
                ? "DECISION: " + decision.Verdict + " — conf " + decision.Confidence.ToString("0.00")
                : "pinch RUN to start";
        }
    }

    /// <summary>
    /// Hand interaction: pinch to point, pinch to select entities, pinch to
    /// press buttons. Falls back to mouse on desktop.
    /// </summary>
    public sealed class WinjayInteraction : MonoBehaviour
    {
        public WinjaySceneController Controller { get; set; }

        private XrEnvironmentSetup _env;
        private WinjayStatusPanel _statusPanel;
        private WinjayInfoCard _infoCard;
        private LineRenderer _laser;
        private GameObject _hitRing;
        private bool _leftWasPinching;
        private bool _rightWasPinching;
        private WinjayButton _hoveredButton;
        private Camera _cam;

        private void SetHover(WinjayButton button)
        {
            if (_hoveredButton == button) return;
            if (_hoveredButton != null) _hoveredButton.SetHover(false);
            _hoveredButton = button;
            if (_hoveredButton != null) _hoveredButton.SetHover(true);
        }

        private void Start()
        {
            _env = GetComponent<XrEnvironmentSetup>();
            _statusPanel = WinjayStatusPanel.Create(transform, Controller);
            _infoCard = WinjayInfoCard.Create(transform);

            var laserGo = new GameObject("HandLaser");
            laserGo.transform.SetParent(transform, false);
            _laser = laserGo.AddComponent<LineRenderer>();
            _laser.material = WinjayPrimitives.CreateUnlit(WinjayPalette.States["Investigating"]);
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
                WinjayPrimitives.CreateUnlit(WinjayPalette.States["Investigating"]);
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
            var hover = Cast(ray);
            SetHover(hover.Button);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (hover.Button != null) hover.Button.Press();
                else if (hover.Tag != null) ShowInfo(hover.Tag, hover.Point);
                else _infoCard.Hide();
            }
            ShowLaser(ray, hover);
        }

        // ------------------------------------------------------------- hands

        private void UpdateHands()
        {
            foreach (var handType in new[] { OVRHand.Hand.HandLeft, OVRHand.Hand.HandRight })
            {
                var hand = _env != null ? _env.GetHand(handType) : null;
                if (hand == null) continue;
                if (!hand.IsTracked) continue;

                var pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
                var wasPinching = handType == OVRHand.Hand.HandLeft ? _leftWasPinching : _rightWasPinching;
                var pose = hand.PointerPose;
                var ray = new Ray(pose.position, pose.forward);

                if (pinching)
                {
                    var hit = Cast(ray);
                    ShowLaser(ray, hit);
                    SetHover(hit.Button);
                    if (!wasPinching)
                    {
                        if (hit.Button != null) hit.Button.Press();
                        else if (hit.Tag != null) ShowInfo(hit.Tag, hit.Point);
                        else _infoCard.Hide();
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
            public WinjayEntityTag Tag;
            public Vector3 Point;
        }

        private Hit Cast(Ray ray)
        {
            var hit = new Hit();
            if (Physics.Raycast(ray, out var info, 25f))
            {
                hit.Point = info.point;
                hit.Button = info.collider.GetComponentInParent<WinjayButton>();
                hit.Tag = info.collider.GetComponentInParent<WinjayEntityTag>();
            }
            return hit;
        }

        private void ShowLaser(Ray ray, Hit hit)
        {
            _laser.enabled = true;
            _laser.SetPosition(0, ray.origin);
            var end = hit.Point != Vector3.zero ? hit.Point : ray.origin + ray.direction * 12f;
            _laser.SetPosition(1, end);
            _hitRing.SetActive(hit.Tag != null || hit.Button != null);
            if (_hitRing.activeSelf)
            {
                _hitRing.transform.position = end;
                _hitRing.transform.localScale = Vector3.one * (hit.Tag != null ? 0.12f : 0.06f);
            }
        }

        private void ShowInfo(WinjayEntityTag tag, Vector3 point)
        {
            if (_infoCard.Target == tag)
            {
                _infoCard.Hide();
                return;
            }
            var summary = Controller != null ? Controller.GetEntitySummary(tag.Id) : string.Empty;
            _infoCard.Show(tag, summary, point);
        }
    }
}
