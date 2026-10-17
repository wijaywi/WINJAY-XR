using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Winjay.Agents;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;

namespace WinjayXR
{
    /// <summary>
    /// Runs the whole Winjay world inside Unity: state engine + mock agents
    /// + scenario runner tick-by-tick, with the spatial view as the only
    /// surface the human sees. No chat — only state.
    /// </summary>
    public sealed class WinjaySceneController : MonoBehaviour
    {
        public enum ScenarioMode { Conflict, Resolve }

        [Header("Scenario")]
        public ScenarioMode scenario = ScenarioMode.Conflict;
        [Tooltip("Pause between ticks (seconds)")]
        public float tickInterval = 0.6f;
        public bool autoRun = true;

        private WinjayEventBus _bus;
        private WinjayStateEngine _engine;
        private ScenarioRunner _runner;
        private WinjayWorldView _view;
        private Coroutine _runCoroutine;
        private Transform _viewRoot;
        private readonly List<WinjayEvent> _pendingEvents = new List<WinjayEvent>();

        public event System.Action<int> ScenarioFinished;
        public event System.Action<WinjayEvent> EventApplied;

        public bool IsRunning => _runCoroutine != null;
        public int TickCount { get; private set; }
        public string ScenarioTitle => scenario == ScenarioMode.Resolve ? "ScenarioResolve" : "DemoScenario01";
        public WinjayWorldView WorldView => _view;
        public WinjayStateEngine Engine => _engine;

        private void Awake()
        {
            BuildWorld();
        }

        private void Update()
        {
            if (_view == null) return;
            _view.Update(Time.deltaTime);
            WinjayEvent[] batch;
            lock (_pendingEvents)
            {
                if (_pendingEvents.Count == 0) return;
                batch = _pendingEvents.ToArray();
                _pendingEvents.Clear();
            }
            foreach (var evt in batch)
            {
                _view.ApplyEvent(evt, _engine);
                EventApplied?.Invoke(evt);
            }
        }

        public void BuildWorld()
        {
            if (_runCoroutine != null)
            {
                StopCoroutine(_runCoroutine);
                _runCoroutine = null;
            }
            lock (_pendingEvents) _pendingEvents.Clear();
            if (_view != null)
            {
                _view.Clear();
                if (_viewRoot != null) UnityEngine.Object.Destroy(_viewRoot.gameObject);
            }

            _bus = new WinjayEventBus();
            _engine = new WinjayStateEngine(_bus);
            var agents = scenario == ScenarioMode.Resolve ? ResolveAgents() : ConflictAgents();
            _runner = new ScenarioRunner(_engine, _bus, agents);
            _viewRoot = new GameObject("WinjayWorld").transform;
            _viewRoot.SetParent(transform, false);
            _view = new WinjayWorldView(_viewRoot);
            _view.SetAgentNames(new[] { "MockResearcher", "MockAnalyzer", "MockFalsifier", "MockDecision" });
            _bus.Subscribe(evt => { lock (_pendingEvents) _pendingEvents.Add(evt); });
            TickCount = 0;
        }

        public void Run()
        {
            if (_runCoroutine != null) return;
            _runCoroutine = StartCoroutine(RunCoroutine());
        }

        public void Stop()
        {
            if (_runCoroutine == null) return;
            StopCoroutine(_runCoroutine);
            _runCoroutine = null;
        }

        public void Step()
        {
            Stop();
            Tick();
        }

        public void Reset()
        {
            BuildWorld();
        }

        public void ToggleScenario()
        {
            scenario = scenario == ScenarioMode.Conflict ? ScenarioMode.Resolve : ScenarioMode.Conflict;
            BuildWorld();
        }

        public string GetEntitySummary(string id)
        {
            if (_engine == null) return string.Empty;
            var entity = _engine.Get<WinjayEntity>(id);
            if (entity == null) return string.Empty;
            var summary = entity.Type + " " + entity.Id;
            switch (entity)
            {
                case Evidence e:
                    summary += " — \"" + e.Content + "\" — source: " + e.Source +
                               " — conf " + e.Confidence.ToString("0.00");
                    break;
                case Hypothesis h:
                    summary += " — state: " + h.State + " — conf " + h.Confidence.ToString("0.00") +
                               "\n\"" + h.Statement + "\"";
                    break;
                case Conflict c:
                    summary += " — " + c.EvidenceId + " contradicts " + c.HypothesisId;
                    break;
                case Decision d:
                    summary += " — " + d.Verdict + " — conf " + d.Confidence.ToString("0.00") +
                               "\n" + d.Reason;
                    break;
            }
            if (entity.Provenance.Count > 0)
            {
                var last = entity.Provenance[entity.Provenance.Count - 1];
                summary += "\nprovenance: " + last.Actor + " → " + last.Action;
            }
            return summary;
        }

        private IEnumerator RunCoroutine()
        {
            while (Tick()) yield return new WaitForSeconds(tickInterval);
            _runCoroutine = null;
            ScenarioFinished?.Invoke(TickCount);
        }

        private bool Tick()
        {
            var advanced = _runner.Tick();
            if (advanced) TickCount++;
            return advanced;
        }

        private static IWinjayAgent[] ConflictAgents()
        {
            return new IWinjayAgent[]
            {
                new MockResearcher("MockResearcher",
                    new EvidenceScriptItem("E001", "Evidence A", 0.81),
                    new EvidenceScriptItem("E002", "Evidence B", 0.70)),
                new MockAnalyzer("MockAnalyzer", "H001", resolveWhenFullySupported: false, "E001", "E002"),
                new MockFalsifier("MockFalsifier", "H001",
                    new EvidenceScriptItem("E003", "Contradicting observation", 0.90)),
                new MockDecision("MockDecision", "H001"),
            };
        }

        private static IWinjayAgent[] ResolveAgents()
        {
            return new IWinjayAgent[]
            {
                new MockResearcher("MockResearcher",
                    new EvidenceScriptItem("E001", "Evidence A", 0.81)),
                new MockAnalyzer("MockAnalyzer", "H001", resolveWhenFullySupported: true, "E001"),
                new MockFalsifier("MockFalsifier", "H001", contradiction: null),
                new MockDecision("MockDecision", "H001"),
            };
        }
    }
}
