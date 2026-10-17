using System.Collections.Generic;
using Winjay.Core.Events;
using Winjay.Core.Proposals;
using Winjay.Core.State;

namespace Winjay.Agents
{
    public sealed class ScenarioStep
    {
        public int Tick { get; internal set; }
        public string Agent { get; internal set; }
        public AgentProposal Proposal { get; internal set; }
        public Result Result { get; internal set; }
    }

    public sealed class ScenarioRunResult
    {
        public int Ticks { get; internal set; }
        public bool Completed { get; internal set; }
        public IReadOnlyList<ScenarioStep> Steps { get; internal set; }
    }

    /// <summary>
    /// Deterministic orchestrator: runs the agent pipeline tick by tick —
    /// Researcher, then Analyzer, then Falsifier, then Decision — applying
    /// each proposal through the state engine, until a full tick produces no
    /// proposals (fixpoint) or MaxTicks is reached.
    /// </summary>
    public sealed class ScenarioRunner
    {
        private readonly WinjayStateEngine _engine;
        private readonly WinjayEventBus _bus;
        private readonly List<IWinjayAgent> _agents;
        private int _tickCount;

        public ScenarioRunner(WinjayStateEngine engine, WinjayEventBus bus, params IWinjayAgent[] agents)
        {
            _engine = engine;
            _bus = bus;
            _agents = new List<IWinjayAgent>(agents ?? new IWinjayAgent[0]);
        }

        public int MaxTicks { get; set; } = 200;

        public IReadOnlyList<IWinjayAgent> Agents => _agents;

        public IReadOnlyList<ScenarioStep> LastSteps { get; private set; } = new List<ScenarioStep>();

        /// <summary>Advances one tick. Returns false when no agent proposed anything.</summary>
        public bool Tick()
        {
            var steps = new List<ScenarioStep>();
            foreach (var agent in _agents)
            {
                // Fresh view per agent so later agents see earlier commits of this tick.
                var view = new WorldView(_engine, _bus);
                foreach (var proposal in agent.Step(view))
                {
                    var result = _engine.Apply(proposal);
                    steps.Add(new ScenarioStep
                    {
                        Tick = _tickCount,
                        Agent = agent.Name,
                        Proposal = proposal,
                        Result = result
                    });
                }
            }
            _tickCount++;
            LastSteps = steps;
            return steps.Count > 0;
        }

        public ScenarioRunResult Run()
        {
            var all = new List<ScenarioStep>();
            var ticks = 0;
            while (ticks < MaxTicks)
            {
                if (!Tick()) break;
                all.AddRange(LastSteps);
                ticks++;
            }
            return new ScenarioRunResult
            {
                Ticks = ticks,
                Completed = ticks < MaxTicks,
                Steps = all
            };
        }
    }
}
