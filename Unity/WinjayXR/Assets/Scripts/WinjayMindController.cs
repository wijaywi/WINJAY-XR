using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Winjay.Agents.Mind;
using Winjay.Core.Mind;

namespace WinjayXR
{
    /// <summary>
    /// Runs the living mind map inside Unity: deterministic MindEngine +
    /// scripted agents + curator story, tick by tick. The spatial view is a
    /// pure fold of the event journal — rewind rebuilds from a shorter
    /// prefix, so everything visible always comes from the log.
    /// </summary>
    public sealed class WinjayMindController : MonoBehaviour
    {
        public enum StoryPhase { Ready, Running, Settled }

        [Header("Story")]
        [Tooltip("Seconds between story ticks")]
        public float tickInterval = 0.5f;
        public bool autoRun = true;

        private DemoMindStory _story;
        private WinjayMindView _view;
        private Transform _viewRoot;
        private Coroutine _runCoroutine;
        private int _seenEvents;
        private int _viewUpto = -1;          // -1 = live; otherwise prefix length
        private int _sayCount;

        public event System.Action<MindEvent> EventApplied;
        public event System.Action<MindStoryStep> StoryAdvanced;
        public event System.Action Rewound;

        public DemoMindStory Story => _story;
        public WinjayMindView View => _view;
        public MindEngine Engine => _story != null ? _story.Engine : null;
        public bool IsRunning => _runCoroutine != null;
        public bool IsLive => _viewUpto < 0;
        public int TickCount => _story != null ? _story.TickCount : 0;
        public int EventCount => _story != null ? _story.Engine.Journal.Count : 0;
        public int ViewUpto => _viewUpto < 0 ? EventCount : Mathf.Min(_viewUpto, EventCount);
        public StoryPhase Phase
        {
            get
            {
                if (_story == null) return StoryPhase.Ready;
                if (_story.Settled) return StoryPhase.Settled;
                return IsRunning ? StoryPhase.Running : StoryPhase.Ready;
            }
        }

        public string StatusLine
        {
            get
            {
                if (_story == null) return "ready";
                if (_story.Settled) return "settled \u2014 " + (_story.SettledReason ?? "map at rest");
                return IsRunning ? "agents working\u2026" : "ready \u2014 press RUN";
            }
        }

        private void Awake()
        {
            BuildWorld();
        }

        private void Start()
        {
            if (autoRun) Run();
        }

        private void Update()
        {
            if (_view == null) return;
            _view.Update(Time.deltaTime);

            if (!IsLive) return; // rewound: the map stays folded at the prefix
            var journal = _story.Engine.Journal;
            while (_seenEvents < journal.Count)
            {
                var evt = journal[_seenEvents++];
                _view.ApplyEvent(evt, false);
                EventApplied?.Invoke(evt);
            }
        }

        // ------------------------------------------------------------ world

        public void BuildWorld()
        {
            StopRun();
            if (_view != null)
            {
                _view.Clear();
                if (_viewRoot != null) Object.Destroy(_viewRoot.gameObject);
            }

            var engine = new MindEngine();
            _story = DemoMindScenario.CreateStory(engine);
            _viewRoot = new GameObject("WinjayMindWorld").transform;
            _viewRoot.SetParent(transform, false);
            _view = new WinjayMindView(_viewRoot);
            _seenEvents = 0;
            _viewUpto = -1;
            _sayCount = 0;
        }

        public void Reset()
        {
            BuildWorld();
        }

        // ------------------------------------------------------------ flow

        public void Run()
        {
            if (_runCoroutine != null) return;
            _runCoroutine = StartCoroutine(RunCoroutine());
        }

        public void StopRun()
        {
            if (_runCoroutine == null) return;
            StopCoroutine(_runCoroutine);
            _runCoroutine = null;
        }

        public void Step()
        {
            StopRun();
            AdvanceOne();
        }

        private IEnumerator RunCoroutine()
        {
            while (AdvanceOne()) yield return new WaitForSeconds(tickInterval);
            _runCoroutine = null;
        }

        private bool AdvanceOne()
        {
            if (_story == null || _story.Settled) return false;
            if (!_story.Step()) return false;
            var steps = _story.Steps;
            if (steps.Count > 0) StoryAdvanced?.Invoke(steps[steps.Count - 1]);
            return true;
        }

        // ------------------------------------------------------------ rewind

        public void Rewind(int upto)
        {
            if (_view == null || _story == null) return;
            var journal = _story.Engine.Journal;
            var clamped = Mathf.Clamp(upto, 0, journal.Count);
            _viewUpto = clamped >= journal.Count ? -1 : clamped;
            _view.FoldTo(journal, clamped);
            Rewound?.Invoke();
        }

        public void GoLive()
        {
            if (_view == null || _story == null) return;
            _viewUpto = -1;
            var journal = _story.Engine.Journal;
            _view.FoldTo(journal, journal.Count);
            _seenEvents = journal.Count;
            Rewound?.Invoke();
        }

        // ------------------------------------------------------------ curator

        public MindResult Prune(string id) => Curate(new PruneMindNode { Actor = "Human", Id = id }, "prunes \u201C" + LabelOf(id) + "\u201D");

        public MindResult ToggleLock(string id)
        {
            var node = Engine != null ? Engine.GetNode(id) : null;
            if (node == null) return MindResult.Fail("NOT_FOUND", "unknown node " + id);
            return node.Locked
                ? Curate(new UnlockMindNode { Actor = "Human", Id = id }, "unlocks \u201C" + LabelOf(id) + "\u201D")
                : Curate(new LockMindNode { Actor = "Human", Id = id }, "locks \u201C" + LabelOf(id) + "\u201D (gold \u2014 agents must work around it)");
        }

        public MindResult AcceptGhost(string id) => Curate(new AcceptMindGhost { Actor = "Human", Id = id }, "accepts \u201C" + LabelOf(id) + "\u201D");

        public MindResult RejectGhost(string id) => Curate(new RejectMindGhost { Actor = "Human", Id = id }, "rejects \u201C" + LabelOf(id) + "\u201D");

        public MindResult Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return MindResult.Fail("EMPTY", "empty speech");
            var engine = Engine;
            var id = "SAY" + (++_sayCount).ToString("00");
            while (engine.GetNode(id) != null) id = "SAY" + (++_sayCount).ToString("00");
            var parent = engine.Root != null ? engine.Root.Id : null;
            var angle = (330 + _sayCount * 15) * Mathf.Deg2Rad;
            var action = new MindHumanInput
            {
                Actor = "Human",
                Id = id,
                Label = text,
                ParentId = parent,
                Position = new MindVec3(3.2f * Mathf.Cos(angle), 0f, 3.2f * Mathf.Sin(angle))
            };
            return Curate(action, "says: \u201C" + text + "\u201D");
        }

        private MindResult Curate(IMindAction action, string note)
        {
            if (_story == null) return MindResult.Fail("NO_STORY", "no story");
            var result = _story.Engine.Apply(action);
            var step = _story.RecordHuman(action, result, note);
            if (step != null) StoryAdvanced?.Invoke(step);
            return result;
        }

        private string LabelOf(string id)
        {
            var node = Engine != null ? Engine.GetNode(id) : null;
            return node != null ? node.Label : id;
        }
    }
}
