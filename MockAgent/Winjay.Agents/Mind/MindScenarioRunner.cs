using System.Collections.Generic;

namespace Winjay.Agents.Mind
{
    public sealed class MindScenarioStep
    {
        public int Tick { get; internal set; }
        public string Agent { get; internal set; }
        public Core.Mind.IMindAction Action { get; internal set; }
        public Core.Mind.MindResult Result { get; internal set; }
        public string Note { get; internal set; }
        public bool Applied => Result != null && Result.Ok;
    }

    public sealed class MindRunResult
    {
        public int Ticks { get; internal set; }
        public bool Settled { get; internal set; }
        public IReadOnlyList<MindScenarioStep> Steps { get; internal set; }
        public string SettledReason { get; internal set; }
    }

    /// <summary>
    /// Deterministic orchestrator for the mind map: on every tick each agent
    /// steps once (at most one action per agent) through a fresh read-only
    /// view, and the engine applies what was proposed. One action per agent
    /// per tick means the map grows at a demo-friendly pace and reactions to
    /// human interventions become visible — real, not animated.
    /// </summary>
    public sealed class MindScenarioRunner
    {
        private readonly Core.Mind.MindEngine _engine;
        private readonly List<IMindAgent> _agents;
        private int _tickCount;

        public MindScenarioRunner(Core.Mind.MindEngine engine, params IMindAgent[] agents)
        {
            _engine = engine;
            _agents = new List<IMindAgent>(agents ?? new IMindAgent[0]);
        }

        public Core.Mind.MindEngine Engine => _engine;

        public IReadOnlyList<IMindAgent> Agents => _agents;

        public int TickCount => _tickCount;

        public int MaxTicks { get; set; } = 600;

        public IReadOnlyList<MindScenarioStep> LastSteps { get; private set; } = new List<MindScenarioStep>();

        /// <summary>Advances one tick. Returns the steps applied this tick (empty means the map is at rest).</summary>
        public IReadOnlyList<MindScenarioStep> TickSteps()
        {
            var steps = new List<MindScenarioStep>();
            foreach (var agent in _agents)
            {
                // Fresh view per agent so later agents see the commits of this tick.
                var view = new MindWorldView(_engine);
                foreach (var intent in agent.Step(view))
                {
                    var result = _engine.Apply(intent.Action);
                    steps.Add(new MindScenarioStep
                    {
                        Tick = _tickCount,
                        Agent = agent.Name,
                        Action = intent.Action,
                        Note = intent.Note,
                        Result = result
                    });
                    break; // one action per agent per tick
                }
            }
            _tickCount++;
            LastSteps = steps;
            return steps;
        }

        /// <summary>Advances one tick. Returns false when no agent proposed anything (fixpoint).</summary>
        public bool Tick()
        {
            return TickSteps().Count > 0;
        }

        public MindRunResult Run()
        {
            var all = new List<MindScenarioStep>();
            var ticks = 0;
            while (ticks < MaxTicks)
            {
                var steps = TickSteps();
                if (steps.Count == 0) break;
                all.AddRange(steps);
                ticks++;
            }
            return new MindRunResult
            {
                Ticks = ticks,
                Settled = ticks < MaxTicks,
                Steps = all,
                SettledReason = ticks < MaxTicks ? "no agent had a ready step" : "tick limit reached"
            };
        }
    }
}
