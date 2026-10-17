using System;
using System.Collections.Generic;
using Winjay.Core.Mind;

namespace Winjay.Agents.Mind
{
    /// <summary>
    /// The prepared demo topic: "3-Day Vacation on a Limited Budget".
    /// Explorer grows the map, Connector finds support/bridge links plus a
    /// ghost suggestion after a prune, Challenger marks collisions, and the
    /// curator (scripted here, live in the XR demo) prunes, locks and speaks.
    /// Every step is guarded by world state — reactions are real, the
    /// opening growth order is scripted.
    /// </summary>
    public static class DemoMindScenario
    {
        public const string TopicLabel = "3-Day Vacation";

        public static MindScenarioRunner CreateRunner()
        {
            return CreateRunner(new MindEngine());
        }

        public static MindScenarioRunner CreateRunner(MindEngine engine)
        {
            return new MindScenarioRunner(
                engine,
                new MockExplorer(ExplorerScript()),
                new MockConnector(ConnectorScript()),
                new MockChallenger(ChallengerScript()));
        }

        /// <summary>The scripted demo story: agents plus the curator's four interventions.</summary>
        public static DemoMindStory CreateStory(MindEngine engine = null)
        {
            return new DemoMindStory(CreateRunner(engine ?? new MindEngine()), CuratorScript());
        }

        // ------------------------------------------------------------- layout

        /// <summary>Polar placement in the ground plane (X-Z), angle in degrees.</summary>
        private static MindVec3 P(double angleDeg, double radius)
        {
            var a = angleDeg * Math.PI / 180.0;
            return new MindVec3((float)(radius * Math.Cos(a)), 0f, (float)(radius * Math.Sin(a)));
        }

        // ------------------------------------------------------------- scripts

        private static MindScriptItem Node(string id, string label, string parent, MindNodeKind kind,
            MindContributor who, double angle, double radius)
        {
            return new MindScriptItem(
                () => new AddMindNode
                {
                    Actor = who.ToString(), Id = id, Label = label, Kind = kind,
                    Contributor = who, ParentId = parent, Position = P(angle, radius)
                },
                w => !w.Exists(id),
                "grows " + label);
        }

        private static MindScriptItem Edge(string id, string from, string to, MindLinkType type,
            MindContributor who, string detail = null, Func<MindWorldView, bool> extraReady = null)
        {
            return new MindScriptItem(
                () => new AddMindLink
                {
                    Actor = who.ToString(), Id = id, FromId = from, ToId = to,
                    Type = type, Originator = who, Detail = detail
                },
                w => w.Alive(from) && w.Alive(to) && !w.HasLink(from, to, type) &&
                     (extraReady == null || extraReady(w)),
                detail ?? (type + ": " + from + " -> " + to));
        }

        private static MindScriptItem[] ExplorerScript()
        {
            var ex = MindContributor.Explorer;
            return new[]
            {
                // The five main branches.
                Node("VAC", "3-Day Vacation", null, MindNodeKind.Root, ex, 0, 0),
                Node("TRN", "Transport", "VAC", MindNodeKind.Branch, ex, 90, 2.2),
                Edge("E_TRN", "VAC", "TRN", MindLinkType.Branch, ex),
                Node("STAY", "Lodging", "VAC", MindNodeKind.Branch, ex, 162, 2.2),
                Edge("E_STAY", "VAC", "STAY", MindLinkType.Branch, ex),
                Node("ACT", "Activities", "VAC", MindNodeKind.Branch, ex, 234, 2.2),
                Edge("E_ACT", "VAC", "ACT", MindLinkType.Branch, ex),
                Node("FOOD", "Food", "VAC", MindNodeKind.Branch, ex, 306, 2.2),
                Edge("E_FOOD", "VAC", "FOOD", MindLinkType.Branch, ex),
                Node("BUD", "Budget", "VAC", MindNodeKind.Branch, ex, 18, 2.2),
                Edge("E_BUD", "VAC", "BUD", MindLinkType.Branch, ex),

                // Transport plans.
                Node("ARR", "Late arrival Day 1", "TRN", MindNodeKind.Idea, ex, 90, 4.6),
                Edge("E_ARR", "TRN", "ARR", MindLinkType.Branch, ex),
                Node("RIDE", "4-hour road trip", "TRN", MindNodeKind.Idea, ex, 102, 4.4),
                Edge("E_RIDE", "TRN", "RIDE", MindLinkType.Branch, ex),

                // Activities.
                Node("TREK", "Sunrise trekking 4 AM", "ACT", MindNodeKind.Idea, ex, 244, 4.6),
                Edge("E_TREK", "ACT", "TREK", MindLinkType.Branch, ex),
                Node("BEACH", "Relaxed beach day", "ACT", MindNodeKind.Idea, ex, 224, 4.4),
                Edge("E_BEACH", "ACT", "BEACH", MindLinkType.Branch, ex),
                Node("NIGHT", "Night market walk", "ACT", MindNodeKind.Idea, ex, 212, 4.8),
                Edge("E_NIGHT", "ACT", "NIGHT", MindLinkType.Branch, ex),
                Node("SCN", "Scenery stops", "ACT", MindNodeKind.Idea, ex, 256, 4.0),
                Edge("E_SCN", "ACT", "SCN", MindLinkType.Branch, ex),

                // Food and lodging.
                Node("STREET", "Street food stall", "FOOD", MindNodeKind.Idea, ex, 306, 4.6),
                Edge("E_STREET", "FOOD", "STREET", MindLinkType.Branch, ex),
                Node("HOMESTAY", "Homestay in the village", "STAY", MindNodeKind.Idea, ex, 172, 4.4),
                Edge("E_HOMESTAY", "STAY", "HOMESTAY", MindLinkType.Branch, ex),
                Node("HOTEL", "City hotel", "STAY", MindNodeKind.Idea, ex, 150, 4.4),
                Edge("E_HOTEL", "STAY", "HOTEL", MindLinkType.Branch, ex),

                // The curator joins: attach their spoken idea to the map.
                Edge("E_MOTION", "VAC", "MOTION", MindLinkType.Branch, ex,
                    "the curator's constraint joins the map"),

                // Reaction: after the motion-sickness collision, grow an alternative.
                Node("TRAIN", "Night train", "TRN", MindNodeKind.Idea, ex, 78, 4.4),
                Edge("E_TRAIN", "TRN", "TRAIN", MindLinkType.Branch, ex, "alternative to the road trip",
                    w => w.HasLink("MOTION", "RIDE", MindLinkType.Conflict)),

                // Settle: three light paths, once every intervention has landed.
                Phase("ARR", "Day 1"), Phase("TRAIN", "Day 1"),
                Phase("BEACH", "Day 2"), Phase("SUNSET", "Day 2"),
                Phase("STREET", "Day 3"), Phase("HOMESTAY", "Day 3")
            };
        }

        private static MindScriptItem Phase(string id, string phase)
        {
            return new MindScriptItem(
                () => new AssignMindPhase { Actor = MindContributor.Explorer.ToString(), Id = id, Phase = phase },
                w => w.Alive(id) && w.GetNode(id).Phase != phase && w.Exists("TRAIN") &&
                     w.GetNode("TREK").Pruned && w.GetNode("BEACH").Locked &&
                     w.Exists("SUNSET") && !w.GetNode("SUNSET").Ghost,
                "settles " + id + " into " + phase);
        }

        private static MindScriptItem[] ConnectorScript()
        {
            var co = MindContributor.Connector;
            return new[]
            {
                Edge("C_STR_BUD", "STREET", "BUD", MindLinkType.Support, co,
                    "local food keeps the budget alive"),
                Edge("C_TREK_SCN", "TREK", "SCN", MindLinkType.Support, co,
                    "the trek pays off the scenery idea"),
                Edge("C_HOME_NIGHT", "HOMESTAY", "NIGHT", MindLinkType.Bridge, co,
                    "village evenings spill into the night market"),

                // Reaction to the curator's prune: the scenery idea lost its
                // support, so the Connector floats a ghost suggestion.
                new MindScriptItem(
                    () => new AddMindNode
                    {
                        Actor = co.ToString(), Id = "SUNSET", Label = "Sunset at the cliff",
                        Kind = MindNodeKind.Idea, Contributor = co, ParentId = "SCN",
                        Position = P(262, 5.2), IsGhost = true
                    },
                    w => w.GetNode("TREK") != null && w.GetNode("TREK").Pruned && !w.Exists("SUNSET"),
                    "suggests a ghost: 'Sunset at the cliff' to keep the scenery idea alive"),

                Edge("C_SUN_SCN", "SUNSET", "SCN", MindLinkType.Support, co,
                    "sunset keeps the scenery idea alive",
                    w => w.Exists("SUNSET") && !w.GetNode("SUNSET").Ghost)
            };
        }

        private static MindScriptItem[] ChallengerScript()
        {
            var ch = MindContributor.Challenger;
            return new[]
            {
                Edge("X_ARR_TREK", "ARR", "TREK", MindLinkType.Conflict, ch,
                    "a 4 AM start collides with the late arrival"),
                Edge("X_BUD_TREK", "BUD", "TREK", MindLinkType.Conflict, ch,
                    "trek fees exceed the budget limit"),
                Edge("X_MOT_RIDE", "MOTION", "RIDE", MindLinkType.Conflict, ch,
                    "the long winding road versus motion sickness")
            };
        }

        /// <summary>
        /// The curator's scripted interventions, in order. In the live XR
        /// demo the human performs these with gestures and voice; guards
        /// ensure each fires at the right moment of the story.
        /// </summary>
        public static IReadOnlyList<MindScriptItem> CuratorScript()
        {
            return new[]
            {
                new MindScriptItem(
                    () => new PruneMindNode { Actor = "Human", Id = "TREK" },
                    w => w.HasLink("ARR", "TREK", MindLinkType.Conflict) &&
                         w.HasLink("BUD", "TREK", MindLinkType.Conflict),
                    "prunes 'Sunrise trekking 4 AM' — the conflict disappears"),

                new MindScriptItem(
                    () => new AcceptMindGhost { Actor = "Human", Id = "SUNSET" },
                    w => w.Exists("SUNSET") && w.GetNode("SUNSET").Ghost,
                    "accepts the ghost suggestion 'Sunset at the cliff'"),

                new MindScriptItem(
                    () => new LockMindNode { Actor = "Human", Id = "BEACH" },
                    w => w.GetNode("TREK") != null && w.GetNode("TREK").Pruned,
                    "locks 'Relaxed beach day' — agents must work around it"),

                new MindScriptItem(
                    () => new MindHumanInput
                    {
                        Actor = "Human", Id = "MOTION", Label = "I easily get motion sick",
                        ParentId = "VAC", Position = P(330, 3.2)
                    },
                    w => w.GetNode("BEACH") != null && w.GetNode("BEACH").Locked,
                    "speaks: 'I easily get motion sick'")
            };
        }
    }

    public sealed class MindStoryStep
    {
        public int Tick { get; internal set; }
        public string Actor { get; internal set; }
        public string Note { get; internal set; }
        public IMindAction Action { get; internal set; }
        public MindResult Result { get; internal set; }
    }

    public sealed class MindStoryResult
    {
        public int Ticks { get; internal set; }
        public bool Settled { get; internal set; }
        public string SettledReason { get; internal set; }
        public IReadOnlyList<MindStoryStep> Steps { get; internal set; }
    }

    /// <summary>
    /// Runs the full scripted demo story — agents tick until quiet, then the
    /// next ready curator step fires, until both sides settle. Step() advances
    /// one unit so web and XR clients can pace it with a timer.
    /// </summary>
    public sealed class DemoMindStory
    {
        private readonly MindScenarioRunner _runner;
        private readonly List<MindScriptItem> _curator;
        private readonly List<MindStoryStep> _steps = new List<MindStoryStep>();

        public DemoMindStory(MindScenarioRunner runner, IReadOnlyList<MindScriptItem> curatorSteps)
        {
            _runner = runner;
            _curator = new List<MindScriptItem>(curatorSteps ?? new MindScriptItem[0]);
        }

        public MindEngine Engine => _runner.Engine;

        public int TickCount => _runner.TickCount;

        public int MaxTicks { get; set; } = 600;

        public bool Settled { get; private set; }

        public string SettledReason { get; private set; }

        public IReadOnlyList<MindStoryStep> Steps => _steps;

        public MindStoryStep LastStep => _steps.Count > 0 ? _steps[_steps.Count - 1] : null;

        /// <summary>Records an action the human performed live (web / XR), keeping the story log complete.</summary>
        public MindStoryStep RecordHuman(IMindAction action, MindResult result, string note)
        {
            var step = new MindStoryStep
            {
                Tick = _runner.TickCount,
                Actor = "Human",
                Note = note,
                Action = action,
                Result = result
            };
            _steps.Add(step);
            return step;
        }

        /// <summary>Advances one unit: an agent tick, or the next ready curator action. False when settled.</summary>
        public bool Step()
        {
            if (Settled) return false;
            if (_runner.TickCount >= MaxTicks)
            {
                Settled = true;
                SettledReason = "tick limit reached";
                return false;
            }

            var agentSteps = _runner.TickSteps();
            if (agentSteps.Count > 0)
            {
                foreach (var step in agentSteps)
                {
                    _steps.Add(new MindStoryStep
                    {
                        Tick = step.Tick, Actor = step.Agent, Note = step.Note,
                        Action = step.Action, Result = step.Result
                    });
                }
                return true;
            }

            var view = new MindWorldView(_runner.Engine);
            var next = null as MindScriptItem;
            foreach (var item in _curator)
            {
                if (item.Ready(view))
                {
                    next = item;
                    break;
                }
            }
            if (next == null)
            {
                Settled = true;
                SettledReason = "agents are at rest and no curator step is pending";
                return false;
            }

            _curator.Remove(next);
            var action = next.Build();
            var result = _runner.Engine.Apply(action);
            _steps.Add(new MindStoryStep
            {
                Tick = _runner.TickCount, Actor = "Human", Note = next.Note, Action = action, Result = result
            });
            return true;
        }

        public MindStoryResult RunUntilSettled()
        {
            while (Step())
            {
            }
            return new MindStoryResult
            {
                Ticks = _runner.TickCount,
                Settled = Settled,
                SettledReason = SettledReason,
                Steps = _steps
            };
        }
    }
}
