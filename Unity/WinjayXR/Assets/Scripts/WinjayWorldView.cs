using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;

namespace WinjayXR
{
    /// <summary>
    /// Spatial renderer for the Winjay state machine. Mirrors the web
    /// visualizer: state = color, conflict = tension, contradiction = jagged
    /// red edge, provenance = flight particle, anchoring = floor lines.
    /// Pure view — reads events and renders; never touches engine state.
    /// </summary>
    public sealed class WinjayWorldView
    {
        private readonly Transform _root;
        private readonly Dictionary<string, EntityView> _views = new Dictionary<string, EntityView>();
        private readonly Dictionary<string, LineRenderer> _supportEdges = new Dictionary<string, LineRenderer>();
        private readonly Dictionary<string, ContradictEdge> _contradictEdges = new Dictionary<string, ContradictEdge>();
        private readonly Dictionary<string, AgentAnchor> _agents = new Dictionary<string, AgentAnchor>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Pulse> _pulses = new List<Pulse>();
        private readonly Transform _labelRoot;

        public WinjayWorldView(Transform root)
        {
            _root = root;
            _labelRoot = new GameObject("Labels").transform;
            _labelRoot.SetParent(root, false);
            BuildGround();
            BuildAgents();
        }

        // ------------------------------------------------------------- scene

        private void BuildGround()
        {
            var grid = new GameObject("GroundGrid");
            grid.transform.SetParent(_root, false);
            const int n = 26;
            const float half = n / 2f;
            for (var i = 0; i <= n; i++)
            {
                var x = -half + i;
                var h = WinjayPrimitives.MakeLine(WinjayPalette.Ground, 0.012f);
                h.transform.SetParent(grid.transform, false);
                h.positionCount = 2;
                h.SetPositions(new[] { new Vector3(x, 0.01f, -half), new Vector3(x, 0.01f, half) });
                var v = WinjayPrimitives.MakeLine(WinjayPalette.Ground, 0.012f);
                v.transform.SetParent(grid.transform, false);
                v.positionCount = 2;
                v.SetPositions(new[] { new Vector3(-half, 0.01f, x), new Vector3(half, 0.01f, x) });
            }
        }

        private void BuildAgents()
        {
            for (var i = 0; i < WinjayPalette.AgentPositions.Length; i++)
            {
                var anchor = new AgentAnchor(_root, WinjayPalette.AgentPositions[i], WinjayPalette.AgentColors[i]);
                _agents[anchor.Name] = anchor;
            }
        }

        public void SetAgentNames(string[] names)
        {
            var i = 0;
            foreach (var name in names)
            {
                if (i >= _agents.Count) break;
                var anchor = _agents.Values.ElementAt(i);
                anchor.SetName(name);
                i++;
            }
        }

        // ------------------------------------------------------------- events

        public void ApplyEvent(WinjayEvent evt, WinjayStateEngine engine)
        {
            switch (evt.Type)
            {
                case WinjayEventType.EntityCreated:
                    SpawnFlight(evt.Actor, evt.EntityId);
                    if (evt.EntityId != null && engine.Entities.TryGetValue(evt.EntityId, out var created))
                        SpawnEntity(created);
                    break;

                case WinjayEventType.StateChanged:
                    ApplyDelta(evt);
                    break;

                case WinjayEventType.ConflictDetected:
                    if (_views.TryGetValue(evt.EntityId ?? string.Empty, out var cx))
                        cx.Flash();
                    Pulse(WinjayPalette.SlotFor(evt.EntityId ?? "CF001"), WinjayPalette.ContradictEdge, 0.6f);
                    break;

                case WinjayEventType.FalsificationStarted:
                    PulseAgent(evt.Actor);
                    Pulse(WinjayPalette.SlotFor(evt.EntityId ?? "H001"), WinjayPalette.States["Falsifying"], 0.6f);
                    break;

                case WinjayEventType.DecisionProposed:
                    PulseAgent(evt.Actor);
                    break;

                case WinjayEventType.DecisionCommitted:
                    if (_views.TryGetValue(evt.EntityId ?? string.Empty, out var dx))
                        dx.Flash();
                    break;

                case WinjayEventType.EvidenceAdded:
                case WinjayEventType.HypothesisCreated:
                    if (_views.TryGetValue(evt.EntityId ?? string.Empty, out var flash))
                        flash.Flash();
                    break;
            }
        }

        private void ApplyDelta(WinjayEvent evt)
        {
            if (evt.Delta == null || !_views.TryGetValue(evt.Delta.EntityId, out var view))
                return;

            foreach (var change in evt.Delta.Changes)
            {
                switch (change.Field)
                {
                    case "state":
                        view.SetState(change.To);
                        break;
                    case "confidence":
                        view.SetConfidence(ParseDouble(change.To));
                        break;
                    case "supporting_evidence":
                        foreach (var id in SplitIds(change.To))
                            AddSupportEdge(id, evt.Delta.EntityId);
                        break;
                    case "contradicting_evidence":
                        foreach (var id in SplitIds(change.To))
                            AddContradictEdge(id, evt.Delta.EntityId);
                        break;
                }
            }
        }

        private static double ParseDouble(string s)
        {
            double v;
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0.5;
        }

        private static IEnumerable<string> SplitIds(string s)
        {
            return (s ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // ------------------------------------------------------------- entities

        private void SpawnEntity(WinjayEntity entity)
        {
            if (_views.ContainsKey(entity.Id)) return;

            EntityView view;
            switch (entity)
            {
                case Evidence e: view = new EvidenceView(_root, e); break;
                case Hypothesis h: view = new HypothesisView(_root, h); break;
                case Conflict c: view = new ConflictView(_root, c); break;
                case Decision d: view = new DecisionView(_root, d); break;
                default: return;
            }
            _views[entity.Id] = view;
            view.Flash();
        }

        private void AddSupportEdge(string evidenceId, string hypothesisId)
        {
            var key = evidenceId + "->" + hypothesisId;
            if (_supportEdges.ContainsKey(key)) return;

            var lr = WinjayPrimitives.MakeLine(WinjayPalette.SupportEdge, 0.03f);
            lr.transform.SetParent(_root, false);
            lr.positionCount = 21;
            var from = WinjayPalette.SlotFor(evidenceId);
            var to = WinjayPalette.SlotFor(hypothesisId);
            for (var i = 0; i <= 20; i++)
            {
                var k = i / 20f;
                var p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
                lr.SetPosition(i, p);
            }
            _supportEdges[key] = lr;
            Pulse(to, WinjayPalette.SupportEdge, 0.4f);
        }

        private void AddContradictEdge(string evidenceId, string hypothesisId)
        {
            var key = evidenceId + "->" + hypothesisId;
            if (_contradictEdges.ContainsKey(key)) return;

            var lr = WinjayPrimitives.MakeLine(WinjayPalette.ContradictEdge, 0.04f);
            lr.transform.SetParent(_root, false);
            lr.positionCount = 8;
            var edge = new ContradictEdge(lr, WinjayPalette.SlotFor(evidenceId), WinjayPalette.SlotFor(hypothesisId));
            _contradictEdges[key] = edge;
            Pulse(WinjayPalette.SlotFor(hypothesisId), WinjayPalette.ContradictEdge, 0.5f);
        }

        // ------------------------------------------------------------- flights / pulses

        private void SpawnFlight(string actor, string entityId)
        {
            var from = AgentAnchorPosition(actor);
            var to = WinjayPalette.SlotFor(entityId ?? "H001");
            var color = AgentColor(actor);
            _flights.Add(new Flight(from, to, color, entityId));
        }

        private Vector3 AgentAnchorPosition(string actor)
        {
            return _agents.TryGetValue(actor ?? string.Empty, out var a) ? a.Position : new Vector3(0, 4, 0);
        }

        private Color AgentColor(string actor)
        {
            return _agents.TryGetValue(actor ?? string.Empty, out var a) ? a.Color : Color.white;
        }

        private void PulseAgent(string actor)
        {
            if (_agents.TryGetValue(actor ?? string.Empty, out var a)) a.Pulse();
        }

        private void Pulse(Vector3 pos, Color color, float size)
        {
            _pulses.Add(new Pulse(pos, color, size));
        }

        // ------------------------------------------------------------- update

        public void Update(float dt)
        {
            foreach (var agent in _agents.Values) agent.Update(dt);

            var conflictActive = false;
            foreach (var view in _views.Values)
            {
                view.Update(dt);
                if (view is HypothesisView hv && hv.IsTense) conflictActive = true;
            }

            foreach (var edge in _contradictEdges.Values)
                edge.Update(dt, conflictActive);

            for (var i = _flights.Count - 1; i >= 0; i--)
            {
                var f = _flights[i];
                if (!f.Update(dt))
                {
                    if (_views.TryGetValue(f.EntityId ?? string.Empty, out var view)) view.Flash();
                    _flights.RemoveAt(i);
                }
            }

            for (var i = _pulses.Count - 1; i >= 0; i--)
            {
                if (!_pulses[i].Update(dt)) _pulses.RemoveAt(i);
            }
        }

        public IEnumerable<EntityView> Views => _views.Values;
        public bool HasView(string id) => _views.ContainsKey(id);
        public EntityView GetView(string id) => _views.TryGetValue(id, out var v) ? v : null;

        public void Clear()
        {
            foreach (var view in _views.Values) view.Destroy();
            _views.Clear();
            foreach (var lr in _supportEdges.Values) UnityEngine.Object.Destroy(lr.gameObject);
            _supportEdges.Clear();
            foreach (var edge in _contradictEdges.Values) UnityEngine.Object.Destroy(edge.Line.gameObject);
            _contradictEdges.Clear();
            _flights.Clear();
            _pulses.Clear();
        }
    }

    // ------------------------------------------------------------------ anchors

    public sealed class AgentAnchor
    {
        private readonly GameObject _go;
        private readonly GameObject _core;
        private readonly TextMesh _label;
        private float _pulse;
        public Vector3 Position { get; }
        public Color Color { get; }
        public string Name { get; private set; }

        public AgentAnchor(Transform parent, Vector3 pos, Color color)
        {
            Position = pos;
            Color = color;
            Name = "Agent";
            _go = new GameObject("AgentAnchor");
            _go.transform.SetParent(parent, false);
            _go.transform.position = pos;

            _core = new GameObject("Core");
            _core.transform.SetParent(_go.transform, false);
            var coreMat = WinjayPalette.CreateLit(color, 0.7f);
            var coreMesh = _core.AddComponent<MeshFilter>();
            coreMesh.sharedMesh = WinjayPrimitives.Octahedron(0.28f);
            _core.AddComponent<MeshRenderer>().sharedMaterial = coreMat;

            var ring = new GameObject("Ring");
            ring.transform.SetParent(_go.transform, false);
            ring.transform.localEulerAngles = new Vector3(90, 0, 0);
            var ringMesh = ring.AddComponent<MeshFilter>();
            ringMesh.sharedMesh = WinjayPrimitives.Ring(0.42f, 0.03f);
            ring.AddComponent<MeshRenderer>().sharedMaterial = WinjayPrimitives.CreateUnlit(color);

            _label = WinjayPrimitives.MakeLabel(Name, color, 0.13f);
            _label.transform.SetParent(_go.transform, false);
            _label.transform.localPosition = new Vector3(0, -0.75f, 0);
            _label.gameObject.AddComponent<Billboard>();
        }

        public void SetName(string name)
        {
            Name = name;
            _label.text = name;
        }

        public void Pulse() { _pulse = 1f; }

        public void Update(float dt)
        {
            _pulse = Mathf.Max(0, _pulse - dt * 2.2f);
            var s = 1f + _pulse * 0.45f * Mathf.Sin(_pulse * Mathf.PI * 3f);
            _core.transform.localScale = Vector3.one * s;
            _core.transform.Rotate(0, dt * 40f, 0, Space.World);
        }
    }

    // ------------------------------------------------------------------ entity views

    public abstract class EntityView
    {
        protected readonly GameObject Root;
        public string Id { get; }
        public abstract string State { get; }
        public abstract double Confidence { get; }
        private float _flash;

        protected EntityView(Transform parent, string id, Vector3 pos)
        {
            Id = id;
            Root = new GameObject(id);
            Root.transform.SetParent(parent, false);
            Root.transform.position = pos;
        }

        protected void AddAnchorLine(Color color)
        {
            var lr = WinjayPrimitives.MakeLine(new Color(color.r, color.g, color.b, 0.22f), 0.015f);
            lr.transform.SetParent(Root.transform, false);
            lr.positionCount = 2;
            lr.SetPositions(new[]
            {
                new Vector3(0, -Root.transform.position.y + 0.02f, 0),
                new Vector3(0, -0.15f, 0),
            });
        }

        protected void MakeInteractable(float sphereRadius)
        {
            var col = Root.AddComponent<SphereCollider>();
            col.radius = sphereRadius;
            Tag();
        }

        protected void MakeInteractable(Vector3 boxSize)
        {
            var col = Root.AddComponent<BoxCollider>();
            col.size = boxSize;
            Tag();
        }

        private void Tag()
        {
            if (Root.GetComponent<WinjayEntityTag>() != null) return;
            var tag = Root.AddComponent<WinjayEntityTag>();
            tag.Id = Id;
        }

        public void Flash() { _flash = 1f; }

        public virtual void SetState(string state) { }
        public virtual void SetConfidence(double confidence) { }
        public virtual bool IsTense => false;

        public virtual void Update(float dt)
        {
            _flash = Mathf.Max(0, _flash - dt * 1.8f);
            if (_flash > 0)
            {
                var s = 1f + _flash * 0.5f;
                Root.transform.localScale = Vector3.one * s;
            }
            else if (Root.transform.localScale != Vector3.one)
            {
                Root.transform.localScale = Vector3.one;
            }
        }

        public virtual void Destroy()
        {
            UnityEngine.Object.Destroy(Root);
        }
    }

    public sealed class EvidenceView : EntityView
    {
        private readonly GameObject _mesh;
        public override string State => "Evidence";
        public override double Confidence { get; }
        public string Source { get; }

        public EvidenceView(Transform parent, Evidence e)
            : base(parent, e.Id, WinjayPalette.SlotFor(e.Id))
        {
            Confidence = e.Confidence;
            Source = e.Source;
            var color = WinjayPalette.EvidenceColorFor(e.Source);

            _mesh = new GameObject("Mesh");
            _mesh.transform.SetParent(Root.transform, false);
            var mf = _mesh.AddComponent<MeshFilter>();
            mf.sharedMesh = WinjayPrimitives.Octahedron(0.22f);
            _mesh.AddComponent<MeshRenderer>().sharedMaterial = WinjayPalette.CreateLit(color);

            var label = WinjayPrimitives.MakeLabel(e.Id + " — " + Truncate(e.Content, 30), color, 0.11f);
            label.transform.SetParent(Root.transform, false);
            label.transform.localPosition = new Vector3(0, -0.6f, 0);
            label.gameObject.AddComponent<Billboard>();

            AddAnchorLine(color);
            MakeInteractable(0.5f);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "…";
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _mesh.transform.Rotate(0, dt * 50f, 0, Space.World);
        }
    }

    public sealed class HypothesisView : EntityView
    {
        private readonly GameObject _mesh;
        private readonly Material _mat;
        private readonly Transform _barFill;
        private readonly TextMesh _barText;
        private string _state = "Created";
        private double _confidence = 0.5;
        private float _phase;
        public override string State => _state;
        public override double Confidence => _confidence;
        public override bool IsTense => _state == "Conflicted" || _state == "Falsifying";

        public HypothesisView(Transform parent, Hypothesis h)
            : base(parent, h.Id, WinjayPalette.SlotFor(h.Id))
        {
            _confidence = h.Confidence;
            var color = StateColor(_state);

            _mesh = new GameObject("Mesh");
            _mesh.transform.SetParent(Root.transform, false);
            _mesh.transform.localScale = Vector3.one * 0.78f;
            var mf = _mesh.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateSphere();
            _mat = WinjayPalette.CreateLit(color, 0.6f);
            _mesh.AddComponent<MeshRenderer>().sharedMaterial = _mat;

            var label = WinjayPrimitives.MakeLabel(h.Id + " — " + Truncate(h.Statement, 34), Color.white, 0.11f);
            label.transform.SetParent(Root.transform, false);
            label.transform.localPosition = new Vector3(0, -0.85f, 0);
            label.gameObject.AddComponent<Billboard>();

            // confidence bar
            var bar = new GameObject("ConfidenceBar");
            bar.transform.SetParent(Root.transform, false);
            bar.transform.localPosition = new Vector3(0, 1.0f, 0);
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(bg.GetComponent<Collider>());
            bg.name = "Bg";
            bg.transform.SetParent(bar.transform, false);
            bg.transform.localScale = new Vector3(1.6f, 0.16f, 1f);
            bg.GetComponent<MeshRenderer>().sharedMaterial = WinjayPrimitives.CreateUnlit(WinjayPalette.PanelBg);

            var fillGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(fillGo.GetComponent<Collider>());
            fillGo.name = "Fill";
            fillGo.transform.SetParent(bar.transform, false);
            fillGo.transform.localPosition = new Vector3(-0.8f + 0.8f * (float)_confidence, 0, -0.001f);
            _barFill = fillGo.transform;
            fillGo.GetComponent<MeshRenderer>().sharedMaterial = WinjayPrimitives.CreateUnlit(BarColor(_confidence));

            _barText = WinjayPrimitives.MakeLabel(_confidence.ToString("0.00"), Color.white, 0.09f);
            _barText.transform.SetParent(bar.transform, false);
            _barText.transform.localPosition = new Vector3(0, 0, -0.002f);
            bar.AddComponent<Billboard>();

            AddAnchorLine(color);
            MakeInteractable(0.55f);
        }

        private static Mesh CreateSphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(go);
            return mesh;
        }

        private static Color StateColor(string state)
        {
            return WinjayPalette.States.TryGetValue(state, out var c) ? c : WinjayPalette.States["Created"];
        }

        private static Color BarColor(double conf)
        {
            return Color.HSVToRGB((float)(conf * 0.33f), 0.7f, 0.9f);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "…";
        }

        public override void SetState(string state)
        {
            _state = state;
            var color = StateColor(state);
            _mat.color = color;
            _mat.SetColor("_EmissionColor", color * 0.6f);
        }

        public override void SetConfidence(double confidence)
        {
            _confidence = confidence;
            _barFill.localPosition = new Vector3(-0.8f + 0.8f * (float)confidence, 0, -0.001f);
            _barFill.localScale = new Vector3(Mathf.Max(0.02f, 1.6f * (float)confidence), 0.16f, 1f);
            _barFill.GetComponent<Renderer>().sharedMaterial.color = BarColor(confidence);
            _barText.text = confidence.ToString("0.00");
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _phase += dt;
            var slot = WinjayPalette.SlotFor(Id);
            if (IsTense)
            {
                var j = 0.06f;
                Root.transform.position = slot + new Vector3(
                    (Mathf.PerlinNoise(_phase * 22f, 0) - 0.5f) * j,
                    (Mathf.PerlinNoise(0, _phase * 22f) - 0.5f) * j,
                    (Mathf.PerlinNoise(_phase * 22f, 7) - 0.5f) * j);
            }
            else
            {
                Root.transform.position = slot;
            }
            if (_state == "Investigating")
            {
                var s = 0.78f * (1f + Mathf.Sin(_phase * 4.2f) * 0.05f);
                _mesh.transform.localScale = Vector3.one * s;
            }
            else if (_mesh.transform.localScale != Vector3.one * 0.78f)
            {
                _mesh.transform.localScale = Vector3.one * 0.78f;
            }
        }
    }

    public sealed class ConflictView : EntityView
    {
        public override string State => "Conflict";
        public override double Confidence => 0;

        public ConflictView(Transform parent, Conflict c)
            : base(parent, c.Id, WinjayPalette.SlotFor(c.Id))
        {
            var color = WinjayPalette.ContradictEdge;
            foreach (var angle in new[] { 45f, -45f })
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.Destroy(bar.GetComponent<Collider>());
                bar.name = "Bar";
                bar.transform.SetParent(Root.transform, false);
                bar.transform.localScale = new Vector3(0.08f, 0.08f, 0.6f);
                bar.transform.localEulerAngles = new Vector3(0, 0, angle);
                bar.GetComponent<MeshRenderer>().sharedMaterial = WinjayPrimitives.CreateUnlit(color);
            }
            var label = WinjayPrimitives.MakeLabel(c.Id, color, 0.1f);
            label.transform.SetParent(Root.transform, false);
            label.transform.localPosition = new Vector3(0, -0.55f, 0);
            label.gameObject.AddComponent<Billboard>();
            AddAnchorLine(color);
            MakeInteractable(new Vector3(0.7f, 0.7f, 0.7f));
        }
    }

    public sealed class DecisionView : EntityView
    {
        private readonly GameObject _mesh;
        public override string State => "Decision";
        public override double Confidence { get; }
        public string Verdict { get; }

        public DecisionView(Transform parent, Decision d)
            : base(parent, d.Id, WinjayPalette.SlotFor(d.Id))
        {
            Confidence = d.Confidence;
            Verdict = d.Verdict;
            var accept = (d.Verdict ?? string.Empty).ToUpperInvariant().Contains("ACCEPT");
            var color = accept ? WinjayPalette.DecisionAccept : WinjayPalette.DecisionReject;

            _mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(_mesh.GetComponent<Collider>());
            _mesh.name = "Mesh";
            _mesh.transform.SetParent(Root.transform, false);
            _mesh.transform.localScale = Vector3.one * 0.44f;
            _mesh.GetComponent<MeshRenderer>().sharedMaterial = WinjayPalette.CreateLit(color);

            var label = WinjayPrimitives.MakeLabel(
                d.Id + " — " + d.Verdict + " " + d.Confidence.ToString("0.00"), color, 0.11f);
            label.transform.SetParent(Root.transform, false);
            label.transform.localPosition = new Vector3(0, -0.6f, 0);
            label.gameObject.AddComponent<Billboard>();

            AddAnchorLine(color);
            MakeInteractable(new Vector3(0.8f, 0.8f, 0.8f));
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            _mesh.transform.Rotate(0, dt * 40f, 0, Space.World);
        }
    }

    // ------------------------------------------------------------------ flight / pulse / edge

    internal sealed class Flight
    {
        private readonly GameObject _go;
        private readonly Vector3 _from;
        private readonly Vector3 _to;
        private readonly float _dur = 0.75f;
        private float _t;
        public string EntityId { get; }

        public Flight(Vector3 from, Vector3 to, Color color, string entityId)
        {
            _from = from;
            _to = to;
            EntityId = entityId;
            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(_go.GetComponent<Collider>());
            _go.transform.localScale = Vector3.one * 0.14f;
            _go.GetComponent<MeshRenderer>().sharedMaterial = WinjayPrimitives.CreateUnlit(color);
        }

        public bool Update(float dt)
        {
            _t += dt / _dur;
            if (_t >= 1f)
            {
                UnityEngine.Object.Destroy(_go);
                return false;
            }
            var p = Vector3.Lerp(_from, _to, _t);
            p.y += Mathf.Sin(_t * Mathf.PI) * 1.1f;
            _go.transform.position = p;
            return true;
        }
    }

    internal sealed class Pulse
    {
        private readonly GameObject _go;
        private float _t;
        private readonly float _dur = 0.7f;
        private readonly float _size;

        public Pulse(Vector3 pos, Color color, float size)
        {
            _size = size;
            _go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(_go.GetComponent<Collider>());
            _go.transform.position = pos + Vector3.up * 0.05f;
            var mat = WinjayPrimitives.CreateUnlit(color);
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                mat = new Material(shader);
                mat.color = color;
                mat.EnableKeyword("_ALPHABLEND_ON");
            }
            _go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            _go.AddComponent<Billboard>();
        }

        public bool Update(float dt)
        {
            _t += dt / _dur;
            if (_t >= 1f)
            {
                UnityEngine.Object.Destroy(_go);
                return false;
            }
            var s = _size * (0.5f + _t * 1.8f);
            _go.transform.localScale = new Vector3(s, s, 1f);
            var c = _go.GetComponent<MeshRenderer>().sharedMaterial.color;
            c.a = 0.9f * (1f - _t);
            _go.GetComponent<MeshRenderer>().sharedMaterial.color = c;
            return true;
        }
    }

    internal sealed class ContradictEdge
    {
        public LineRenderer Line { get; }
        private readonly Vector3 _from;
        private readonly Vector3 _to;
        private float _phase;

        public ContradictEdge(LineRenderer line, Vector3 from, Vector3 to)
        {
            Line = line;
            _from = from;
            _to = to;
        }

        public void Update(float dt, bool active)
        {
            _phase += dt;
            for (var i = 0; i < Line.positionCount; i++)
            {
                var k = i / (float)(Line.positionCount - 1);
                var p = Vector3.Lerp(_from, _to, k);
                if (active && i > 0 && i < Line.positionCount - 1)
                {
                    var jitter = 0.09f + 0.05f * Mathf.Sin(_phase * 18f + i * 1.7f);
                    p.x += jitter;
                    p.y += Mathf.Sin(_phase * 21f + i * 2.3f) * 0.06f;
                }
                Line.SetPosition(i, p);
            }
        }
    }
}
