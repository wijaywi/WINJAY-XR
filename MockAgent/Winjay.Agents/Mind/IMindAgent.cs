using System;
using System.Collections.Generic;
using Winjay.Core.Mind;

namespace Winjay.Agents.Mind
{
    /// <summary>
    /// What an agent wants to do, with a human-readable note about why —
    /// the note feeds the event feed (and later, LLM agents put their
    /// reasoning here).
    /// </summary>
    public sealed class MindIntent
    {
        public MindIntent(IMindAction action, string note = null)
        {
            Action = action;
            Note = note;
        }

        public IMindAction Action { get; }
        public string Note { get; }
    }

    /// <summary>
    /// Contract shared by mock mind agents and, later, real LLM-backed agents:
    /// observe the map, propose one step at a time.
    /// </summary>
    public interface IMindAgent
    {
        string Name { get; }

        IEnumerable<MindIntent> Step(MindWorldView world);
    }

    /// <summary>
    /// One scripted step with a readiness guard. When the guard is not
    /// satisfied the agent waits (ordered script); reactive steps use guards
    /// that fire only after the world reaches the right state — for example
    /// a ghost suggestion that appears only after the curator prunes a node.
    /// </summary>
    public sealed class MindScriptItem
    {
        private readonly Func<IMindAction> _build;
        private readonly Func<MindWorldView, bool> _ready;

        public MindScriptItem(Func<IMindAction> build, Func<MindWorldView, bool> ready = null, string note = null)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _ready = ready;
            Note = note;
        }

        /// <summary>Free-form description of this step, shown in logs and the demo narration.</summary>
        public string Note { get; }

        public bool Ready(MindWorldView world)
        {
            return _ready == null || _ready(world);
        }

        public IMindAction Build()
        {
            return _build();
        }
    }

    /// <summary>
    /// Shared plumbing for scripted agents: walk the script in order, skip
    /// nothing, wait on guards, emit at most one action per step call.
    /// </summary>
    public abstract class ScriptedMindAgent : IMindAgent
    {
        private readonly IReadOnlyList<MindScriptItem> _script;
        private int _cursor;

        protected ScriptedMindAgent(string name, IReadOnlyList<MindScriptItem> script)
        {
            Name = name;
            _script = script ?? new MindScriptItem[0];
        }

        public string Name { get; }

        public IEnumerable<MindIntent> Step(MindWorldView world)
        {
            while (_cursor < _script.Count)
            {
                var item = _script[_cursor];
                if (!item.Ready(world))
                {
                    yield break;
                }
                _cursor++;
                yield return new MindIntent(item.Build(), item.Note);
                yield break;
            }
        }
    }
}
