using System.Collections.Generic;
using UnityEngine;
using Winjay.Core.Mind;

namespace WinjayXR
{
    /// <summary>
    /// Spatial renderer for the living mind map. Pure fold of the event
    /// journal: nodes are spheres colored by contributor, strength shows as
    /// a ring, locked nodes get a gold ring, days get floor discs, conflicts
    /// vibrate, bridges arc, ghosts pulse. Nothing here invents state — every
    /// visual comes from an applied event.
    /// </summary>
    public sealed class WinjayMindView
    {
        // Chest-height band: the map floats at eye level, only day discs rest on the floor.
        private const float NodeLift = 1.15f;

        private sealed class NodeVisual
        {
            public string Id;
            public string Label;
            public MindNodeKind Kind;
            public MindContributor Contributor;
            public Vector3 TargetPos;
            public GameObject Root;
            public Transform Sphere;
            public Material SphereMat;
            public Transform Ring;
            public Material RingMat;
            public Transform LockRing;
            public Material LockMat;
            public Transform Disc;
            public Material DiscMat;
            public TextMesh LabelMesh;
            public float Strength = 0.5f;
            public bool Locked, Ghost, Pruned, Accepted;
            public string Phase;
            public float SpawnT = 1f;
            public Vector3 SpawnFrom;
            public float PruneT = -1f;
        }

        private sealed class LinkVisual
        {
            public string Id;
            public string FromId, ToId;
            public MindLinkType Type;
            public LineRenderer Line;
            public float Fade;
            public bool Active = true;
            public float PopT;
        }

        private readonly Transform _root;
        private readonly Dictionary<string, NodeVisual> _nodes = new Dictionary<string, NodeVisual>();
        private readonly Dictionary<string, LinkVisual> _links = new Dictionary<string, LinkVisual>();
        private readonly List<LinkVisual> _vibrators = new List<LinkVisual>();

        private readonly Mesh _ringMesh;
        private readonly Mesh _discMesh;
        private readonly Material _lineTemplate;

        private float _time;
        private string _hoverId;
        private string _selectedId;

        public WinjayMindView(Transform root)
        {
            _root = root;
            _ringMesh = WinjayPrimitives.Ring(0.24f, 0.012f, 56);
            _discMesh = WinjayPrimitives.Ring(0.42f, 0.06f, 56);
            var shader = Shader.Find("Sprites/Default");
            _lineTemplate = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            BuildGround();
        }

        public int AliveCount
        {
            get
            {
                var n = 0;
                foreach (var v in _nodes.Values) if (!v.Pruned) n++;
                return n;
            }
        }

        public Vector3 WorldPos(string id)
        {
            NodeVisual v;
            return _nodes.TryGetValue(id ?? string.Empty, out v) ? v.TargetPos : new Vector3(0f, NodeLift, 0f);
        }

        public void SetHover(string id) { _hoverId = id; }
        public void SetSelected(string id) { _selectedId = id; }

        // ------------------------------------------------------------- ground

        private void BuildGround()
        {
            var grid = new GameObject("MindGroundGrid");
            grid.transform.SetParent(_root, false);
            const int half = 8;
            for (var i = -half; i <= half; i++)
            {
                var h = WinjayPrimitives.MakeLine(WinjayPalette.Ground, 0.012f);
                h.transform.SetParent(grid.transform, false);
                h.positionCount = 2;
                h.SetPositions(new[] { new Vector3(i, 0.008f, -half), new Vector3(i, 0.008f, half) });
                var v = WinjayPrimitives.MakeLine(WinjayPalette.Ground, 0.012f);
                v.transform.SetParent(grid.transform, false);
                v.positionCount = 2;
                v.SetPositions(new[] { new Vector3(-half, 0.008f, i), new Vector3(half, 0.008f, i) });
            }
        }

        // ------------------------------------------------------------- events

        public void ApplyEvent(MindEvent evt, bool instant)
        {
            switch (evt.Type)
            {
                case MindEventType.NodeAdded:
                case MindEventType.HumanInput:
                case MindEventType.GhostSuggested:
                    CreateNode(evt, instant);
                    break;
                case MindEventType.LinkCreated:
                    CreateLink(evt, instant);
                    break;
                case MindEventType.LinkRewired:
                {
                    LinkVisual lv;
                    if (evt.LinkId != null && _links.TryGetValue(evt.LinkId, out lv))
                    {
                        lv.FromId = evt.FromId;
                        lv.ToId = evt.ToId;
                    }
                    break;
                }
                case MindEventType.StrengthChanged:
                {
                    NodeVisual nv;
                    if (evt.NodeId != null && _nodes.TryGetValue(evt.NodeId, out nv) && evt.NewStrength.HasValue)
                        nv.Strength = (float)evt.NewStrength.Value;
                    break;
                }
                case MindEventType.NodePruned:
                case MindEventType.GhostRejected:
                case MindEventType.NodeMerged:
                    PruneVisual(evt.NodeId, instant);
                    break;
                case MindEventType.LinkDeactivated:
                {
                    LinkVisual lv;
                    if (evt.LinkId != null && _links.TryGetValue(evt.LinkId, out lv))
                    {
                        lv.Active = false;
                        if (instant) DestroyLink(evt.LinkId);
                    }
                    break;
                }
                case MindEventType.NodeLocked:
                {
                    NodeVisual nv;
                    if (evt.NodeId != null && _nodes.TryGetValue(evt.NodeId, out nv)) nv.Locked = true;
                    break;
                }
                case MindEventType.NodeUnlocked:
                {
                    NodeVisual nv;
                    if (evt.NodeId != null && _nodes.TryGetValue(evt.NodeId, out nv)) nv.Locked = false;
                    break;
                }
                case MindEventType.GhostAccepted:
                {
                    NodeVisual nv;
                    if (evt.NodeId != null && _nodes.TryGetValue(evt.NodeId, out nv))
                    {
                        nv.Ghost = false;
                        nv.Accepted = true;
                        ApplySphereColor(nv);
                    }
                    break;
                }
                case MindEventType.PhaseAssigned:
                {
                    NodeVisual nv;
                    if (evt.NodeId != null && _nodes.TryGetValue(evt.NodeId, out nv))
                    {
                        nv.Phase = evt.Phase;
                        Color c;
                        if (evt.Phase != null && WinjayPalette.MindDays.TryGetValue(evt.Phase, out c))
                        {
                            nv.DiscMat.color = new Color(c.r, c.g, c.b, instant ? 0.30f : 0f);
                            nv.Disc.gameObject.SetActive(true);
                        }
                    }
                    break;
                }
            }
        }

        public void FoldTo(IReadOnlyList<MindEvent> journal, int upto)
        {
            Clear();
            var n = Mathf.Clamp(upto, 0, journal.Count);
            for (var i = 0; i < n; i++) ApplyEvent(journal[i], true);
            // settle every visual at its folded value
            foreach (var v in _nodes.Values)
            {
                v.SpawnT = 1f;
                v.Root.transform.position = v.TargetPos;
                v.Root.transform.localScale = Vector3.one * (v.Pruned ? 0f : 1f);
            }
            foreach (var l in _links.Values) l.Fade = l.Active ? 1f : 0f;
        }

        public void Clear()
        {
            foreach (var id in new List<string>(_nodes.Keys))
            {
                var v = _nodes[id];
                if (v.Root != null) Object.Destroy(v.Root);
            }
            foreach (var id in new List<string>(_links.Keys))
            {
                var l = _links[id];
                if (l.Line != null) Object.Destroy(l.Line.gameObject);
            }
            _nodes.Clear();
            _links.Clear();
            _vibrators.Clear();
            _selectedId = null;
            _hoverId = null;
        }

        // ------------------------------------------------------------- nodes

        private void CreateNode(MindEvent evt, bool instant)
        {
            if (evt.NodeId == null || _nodes.ContainsKey(evt.NodeId)) return;
            var contributor = evt.Contributor ?? MindContributor.Explorer;
            var kind = evt.Kind ?? MindNodeKind.Idea;
            var pos = evt.Position.HasValue
                ? new Vector3(evt.Position.Value.X, NodeLift, evt.Position.Value.Z)
                : new Vector3(0f, NodeLift, 0f);

            var radius = kind == MindNodeKind.Root ? 0.17f : kind == MindNodeKind.Branch ? 0.13f : 0.11f;
            var ghost = evt.Type == MindEventType.GhostSuggested || (evt.Ghost.HasValue && evt.Ghost.Value);

            var v = new NodeVisual
            {
                Id = evt.NodeId,
                Label = evt.Label ?? evt.NodeId,
                Kind = kind,
                Contributor = contributor,
                TargetPos = pos,
                Strength = ghost ? (float)MindEngine.GhostStrength
                    : contributor == MindContributor.Human ? (float)MindEngine.HumanBaseStrength : (float)MindEngine.BaseStrength,
                Ghost = ghost
            };

            v.Root = new GameObject("MindNode_" + v.Id);
            v.Root.transform.SetParent(_root, false);
            v.Root.transform.position = pos;
            v.Root.AddComponent<MindNodeTag>().Id = v.Id;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            sphere.transform.SetParent(v.Root.transform, false);
            sphere.transform.localScale = Vector3.one * (radius * 2f);
            v.Sphere = sphere.transform;
            v.SphereMat = WinjayPalette.CreateLit(ContributorColor(contributor), kind == MindNodeKind.Root ? 0.35f : 0.55f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = v.SphereMat;
            if (ghost) MakeGhostly(v);

            // Strength and lock read as camera-facing halos around the sphere;
            // only the day disc stays flat on the floor.
            v.Ring = MakeHalo(v.Root.transform, _ringMesh, out v.RingMat, Color.white, 0.85f);
            v.LockRing = MakeHalo(v.Root.transform, _ringMesh, out v.LockMat, WinjayPalette.MindLock, 0f);
            v.LockRing.localScale = Vector3.one * 1.45f;
            v.Disc = MakeFlatRing(v.Root.transform, _discMesh, 0.012f, out v.DiscMat, Color.white, 0f);
            v.Disc.gameObject.SetActive(false);

            v.LabelMesh = WinjayPrimitives.MakeLabel(v.Label, kind == MindNodeKind.Root ? Color.white : new Color(0.88f, 0.92f, 0.97f), 0.062f, 44);
            v.LabelMesh.transform.SetParent(v.Root.transform, false);
            v.LabelMesh.transform.localPosition = new Vector3(0f, radius + 0.22f + LabelTier(v.Id), 0f);
            v.LabelMesh.gameObject.AddComponent<Billboard>();
            // Node roots rest at scale 1 but animate (spawn / prune), so the
            // label keeps inheriting that scale: fit without counter-scale.
            WinjayPrimitives.FitLabel(v.LabelMesh, 0.92f, 0.10f, false);

            if (instant)
            {
                v.SpawnT = 1f;
            }
            else
            {
                v.SpawnT = 0f;
                var parentId = evt.ParentId;
                v.SpawnFrom = parentId != null && _nodes.ContainsKey(parentId)
                    ? _nodes[parentId].TargetPos
                    : new Vector3(0f, NodeLift, 0f);
                v.Root.transform.position = v.SpawnFrom;
            }
            _nodes[v.Id] = v;
        }

        private void MakeGhostly(NodeVisual v)
        {
            var mat = WinjayPalette.CreateUnlitTransparent(ContributorColor(v.Contributor), 0.35f);
            v.Sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;
            v.SphereMat = mat;
        }

        private void ApplySphereColor(NodeVisual v)
        {
            var mat = v.Ghost
                ? WinjayPalette.CreateUnlitTransparent(ContributorColor(v.Contributor), 0.35f)
                : WinjayPalette.CreateLit(ContributorColor(v.Contributor), v.Kind == MindNodeKind.Root ? 0.35f : 0.55f);
            v.Sphere.GetComponent<MeshRenderer>().sharedMaterial = mat;
            v.SphereMat = mat;
        }

        private static Transform MakeFlatRing(Transform parent, Mesh mesh, float y, out Material mat, Color color, float alpha)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y - NodeLift, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mat = WinjayPalette.CreateUnlitTransparent(color, alpha);
            mr.sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>Camera-facing ring centred on the sphere — readable from any eye height.</summary>
        private static Transform MakeHalo(Transform parent, Mesh mesh, out Material mat, Color color, float alpha)
        {
            var go = new GameObject("Halo");
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mat = WinjayPalette.CreateUnlitTransparent(color, alpha);
            mr.sharedMaterial = mat;
            go.AddComponent<Billboard>();
            return go.transform;
        }

        /// <summary>Deterministic 3-tier stagger so neighbouring labels do not collide.</summary>
        private static float LabelTier(string id)
        {
            unchecked
            {
                var h = 2166136261u;
                for (var i = 0; i < id.Length; i++) h = (h ^ id[i]) * 16777619u;
                return (h % 3u) * 0.18f;
            }
        }

        private void PruneVisual(string id, bool instant)
        {
            NodeVisual v;
            if (id == null || !_nodes.TryGetValue(id, out v) || v.Pruned) return;
            v.Pruned = true;
            foreach (var l in _links.Values)
            {
                if (l.FromId == id || l.ToId == id) l.Active = false;
            }
            if (instant) DestroyNode(id);
            else v.PruneT = 0f;
        }

        private void DestroyNode(string id)
        {
            NodeVisual v;
            if (!_nodes.TryGetValue(id, out v)) return;
            if (v.Root != null) Object.Destroy(v.Root);
            _nodes.Remove(id);
        }

        private void DestroyLink(string id)
        {
            LinkVisual l;
            if (!_links.TryGetValue(id, out l)) return;
            if (l.Line != null) Object.Destroy(l.Line.gameObject);
            _links.Remove(id);
            _vibrators.Remove(l);
        }

        // ------------------------------------------------------------- links

        private void CreateLink(MindEvent evt, bool instant)
        {
            if (evt.LinkId == null || _links.ContainsKey(evt.LinkId)) return;
            if (evt.FromId == null || evt.ToId == null) return;
            if (!_nodes.ContainsKey(evt.FromId) || !_nodes.ContainsKey(evt.ToId)) return;

            var width = evt.LinkType == MindLinkType.Conflict ? 0.040f
                : evt.LinkType == MindLinkType.Support ? 0.034f
                : evt.LinkType == MindLinkType.Bridge ? 0.032f : 0.020f;

            var lr = WinjayPrimitives.MakeLine(EdgeColor(evt.LinkType), width);
            lr.material = _lineTemplate;
            lr.transform.SetParent(_root, false);
            lr.useWorldSpace = true;
            lr.positionCount = 21;

            var lv = new LinkVisual
            {
                Id = evt.LinkId,
                FromId = evt.FromId,
                ToId = evt.ToId,
                Type = evt.LinkType ?? MindLinkType.Branch,
                Line = lr,
                Fade = instant ? 1f : 0f,
                PopT = instant ? 1f : 0f
            };
            _links[lv.Id] = lv;
            if (lv.Type == MindLinkType.Conflict) _vibrators.Add(lv);
            UpdateLinkPositions(lv, 0f);
        }

        private static Color EdgeColor(MindLinkType? type)
        {
            switch (type)
            {
                case MindLinkType.Support: return WinjayPalette.MindEdgeSupport;
                case MindLinkType.Conflict: return WinjayPalette.MindEdgeConflict;
                case MindLinkType.Bridge: return WinjayPalette.MindEdgeBridge;
                default: return WinjayPalette.MindEdgeBranch;
            }
        }

        private void UpdateLinkPositions(LinkVisual lv, float shake)
        {
            NodeVisual a, b;
            if (!_nodes.TryGetValue(lv.FromId, out a) || !_nodes.TryGetValue(lv.ToId, out b))
            {
                lv.Line.enabled = false;
                return;
            }
            lv.Line.enabled = true;

            var pa = a.Root.transform.position;
            var pb = b.Root.transform.position;
            var arc = lv.Type == MindLinkType.Bridge ? 0.6f : 0f;
            var dir = (pb - pa).normalized;
            var perp = Vector3.Cross(dir, Vector3.up).normalized;

            for (var i = 0; i <= 20; i++)
            {
                var k = i / 20f;
                var p = Vector3.Lerp(pa, pb, k);
                if (arc > 0f) p.y += Mathf.Sin(k * Mathf.PI) * arc;
                if (shake != 0f) p += perp * (Mathf.Sin(k * Mathf.PI) * shake);
                lv.Line.SetPosition(i, p);
            }
        }

        // ------------------------------------------------------------- update

        public void Update(float dt)
        {
            _time += dt;

            // resolved-node blink: the weakest unassigned idea once days exist
            NodeVisual unresolved = null;
            foreach (var v in _nodes.Values)
            {
                if (v.Pruned || v.Ghost || v.Phase != null) continue;
                if (v.Kind != MindNodeKind.Idea) continue;
                if (unresolved == null || v.Strength < unresolved.Strength) unresolved = v;
            }
            var anyPhase = false;
            foreach (var v in _nodes.Values) if (v.Phase != null) { anyPhase = true; break; }
            if (!anyPhase) unresolved = null;

            List<string> finished = null;
            foreach (var v in _nodes.Values)
            {
                if (v.PruneT >= 0f)
                {
                    v.PruneT += dt * 2.2f;
                    var k = 1f - Mathf.Clamp01(v.PruneT);
                    v.Root.transform.localScale = Vector3.one * (k * k);
                    if (v.PruneT >= 1f)
                    {
                        if (finished == null) finished = new List<string>();
                        finished.Add(v.Id);
                    }
                    continue;
                }

                if (v.SpawnT < 1f)
                {
                    v.SpawnT = Mathf.Min(1f, v.SpawnT + dt * 1.35f);
                    var e = 1f - Mathf.Pow(1f - v.SpawnT, 3f);
                    v.Root.transform.position = Vector3.Lerp(v.SpawnFrom, v.TargetPos, e);
                    var s = 0.25f + 0.75f * e;
                    v.Root.transform.localScale = Vector3.one * s;
                }

                var pulse = 1f;
                if (v.Ghost)
                {
                    v.SphereMat.color = WithAlpha(ContributorColor(v.Contributor), 0.25f + 0.18f * Mathf.Sin(_time * 2.6f));
                    pulse = 1f + 0.05f * Mathf.Sin(_time * 2.6f);
                }

                var ringScale = 0.55f + 0.75f * Mathf.Clamp01(v.Strength);
                v.Ring.localScale = new Vector3(ringScale * pulse, ringScale * pulse, 1f);
                var low = v.Strength < 0.35f;
                var blink = unresolved == v && Mathf.Sin(_time * 5f) > 0f;
                var ringColor = low ? WinjayPalette.MindEdgeConflict : new Color(0.92f, 0.96f, 1f);
                if (unresolved == v) ringColor = blink ? WinjayPalette.MindEdgeConflict : new Color(0.92f, 0.96f, 1f);
                v.RingMat.color = WithAlpha(ringColor, v.Pruned ? 0f : 0.8f);

                var lockTarget = v.Locked ? 0.95f : 0f;
                v.LockMat.color = WithAlpha(WinjayPalette.MindLock, Mathf.Lerp(v.LockMat.color.a, lockTarget, Mathf.Min(1f, dt * 5f)));
                v.LockRing.gameObject.SetActive(v.LockMat.color.a > 0.02f);

                if (v.Phase != null)
                {
                    Color day;
                    if (WinjayPalette.MindDays.TryGetValue(v.Phase, out day))
                        v.DiscMat.color = WithAlpha(day, Mathf.Lerp(v.DiscMat.color.a, 0.30f, Mathf.Min(1f, dt * 4f)));
                }

                var selected = v.Id == _selectedId;
                var hover = v.Id == _hoverId;
                if (!v.Ghost)
                {
                    var emission = v.Kind == MindNodeKind.Root ? 0.35f : 0.55f;
                    if (selected) emission = 1.0f;
                    else if (hover) emission = 0.8f;
                    v.SphereMat.SetColor("_EmissionColor", ContributorColor(v.Contributor) * emission);
                }
            }

            if (finished != null)
            {
                foreach (var id in finished) DestroyNode(id);
            }

            foreach (var lv in _links.Values)
            {
                var target = lv.Active ? 1f : 0f;
                lv.Fade = Mathf.Lerp(lv.Fade, target, Mathf.Min(1f, dt * 3.5f));
                if (lv.PopT < 1f) lv.PopT = Mathf.Min(1f, lv.PopT + dt * 3f);
                var alpha = lv.Fade * lv.PopT;
                var c = EdgeColor(lv.Type);
                lv.Line.startColor = WithAlpha(c, alpha);
                lv.Line.endColor = WithAlpha(c, alpha);
                if (alpha <= 0.02f)
                {
                    if (!lv.Active && lv.Fade < 0.03f) { DestroyLink(lv.Id); break; }
                    lv.Line.enabled = false;
                    continue;
                }
                var shake = 0f;
                if (lv.Type == MindLinkType.Conflict && lv.Active && lv.Fade > 0.3f)
                    shake = Mathf.Sin(_time * 26f + lv.Id.Length * 1.7f) * 0.045f + Mathf.Sin(_time * 41f) * 0.011f;
                UpdateLinkPositions(lv, shake);
            }
        }

        private static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        private static Color ContributorColor(MindContributor contributor)
        {
            switch (contributor)
            {
                case MindContributor.Connector: return WinjayPalette.MindConnector;
                case MindContributor.Challenger: return WinjayPalette.MindChallenger;
                case MindContributor.Human: return WinjayPalette.MindHuman;
                default: return WinjayPalette.MindExplorer;
            }
        }
    }

    /// <summary>Marks a spatial mind node so raycasts can identify it.</summary>
    public sealed class MindNodeTag : MonoBehaviour
    {
        public string Id { get; set; }
    }
}
