using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Winjay.Agents;
using Winjay.Agents.Mind;
using Winjay.Core.Events;
using Winjay.Core.Mind;
using Winjay.Core.Model;
using Winjay.Core.State;

namespace Winjay.Host
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var stepMode = args.Any(a => a == "--step");

            if (args.Any(a => a == "--mind"))
            {
                return MindShow(stepMode);
            }

            var scenario = args.Any(a => a == "--resolve") ? "resolve" : "conflict";

            var bus = new WinjayEventBus();
            var engine = new WinjayStateEngine(bus);
            var journal = new DeltaJournal(bus);

            IWinjayAgent[] agents = scenario == "conflict" ? ConflictAgents() : ResolveAgents();
            var runner = new ScenarioRunner(engine, bus, agents);

            PrintBanner(scenario, agents);

            if (stepMode)
            {
                Console.WriteLine("  --step: press Enter for the next tick, 'q' to quit --\n");
                while (true)
                {
                    if (!runner.Tick()) break;
                    PrintSteps(runner.LastSteps);
                    Console.WriteLine();
                    var key = Console.ReadLine();
                    if (key != null && key.Trim().ToLowerInvariant() == "q") break;
                }
                Console.WriteLine("  (no more proposals — fixpoint reached)");
            }
            else
            {
                var result = runner.Run();
                PrintSteps(result.Steps);
                Console.WriteLine();
                Console.WriteLine($"  Finished in {result.Ticks} ticks, {result.Steps.Count} proposals, " +
                                  $"{(result.Completed ? "fixpoint reached" : "MAX TICKS REACHED!")}");
            }

            PrintStateTrail(engine, bus);
            PrintDeltaSummary(journal);
            return 0;
        }

        // ------------------------------------------------------------ scenarios

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

        // ------------------------------------------------------------ printing

        private static void PrintBanner(string scenario, IReadOnlyList<IWinjayAgent> agents)
        {
            var title = scenario == "conflict" ? "DemoScenario01 (conflict)" : "ScenarioResolve (survival)";
            Console.WriteLine();
            Console.WriteLine("  ==============================================================");
            Console.WriteLine("   WINJAY XR — models talk to models, humans watch the state");
            Console.WriteLine($"   {title}");
            Console.WriteLine("  --------------------------------------------------------------");
            Console.WriteLine("   " + string.Join(" -> ", agents.Select(a => a.Name)));
            Console.WriteLine("  ==============================================================");
            Console.WriteLine();
        }

        private static void PrintSteps(IEnumerable<ScenarioStep> steps)
        {
            foreach (var step in steps)
            {
                var p = step.Proposal;
                var r = step.Result;
                var color = r.Ok ? ConsoleColor.Green : ConsoleColor.Red;
                var status = r.Ok ? "OK" : "REJECTED (" + r.ErrorCode + ")";
                var desc = Describe(p);
                Console.Write($"  [tick {step.Tick:00}] {step.Agent,-14} {p.Kind,-18} {desc,-46} ");
                WriteColored(status, color);
                Console.WriteLine();
            }
        }

        private static string Describe(Winjay.Core.Proposals.AgentProposal p)
        {
            switch (p.Kind)
            {
                case Winjay.Core.Proposals.ProposalKind.CreateEvidence:
                    var ce = (Winjay.Core.Proposals.CreateEvidenceProposal)p;
                    return $"{ce.EvidenceId} \"{ce.Content}\" ({ce.Confidence:0.00})";
                case Winjay.Core.Proposals.ProposalKind.CreateHypothesis:
                    var ch = (Winjay.Core.Proposals.CreateHypothesisProposal)p;
                    return $"{ch.HypothesisId} \"{ch.Statement}\"";
                case Winjay.Core.Proposals.ProposalKind.Support:
                    var sp = (Winjay.Core.Proposals.SupportProposal)p;
                    return $"{sp.HypothesisId} <- {sp.EvidenceId}";
                case Winjay.Core.Proposals.ProposalKind.Contradict:
                    var cn = (Winjay.Core.Proposals.ContradictProposal)p;
                    return $"{cn.HypothesisId} X {cn.EvidenceId}";
                case Winjay.Core.Proposals.ProposalKind.StartInvestigation:
                    return p.TargetId;
                case Winjay.Core.Proposals.ProposalKind.StartFalsification:
                    return p.TargetId;
                case Winjay.Core.Proposals.ProposalKind.Fail:
                    return p.TargetId;
                case Winjay.Core.Proposals.ProposalKind.Resolve:
                    return p.TargetId;
                case Winjay.Core.Proposals.ProposalKind.ProposeDecision:
                    var pd = (Winjay.Core.Proposals.ProposeDecisionProposal)p;
                    return $"{pd.Verdict} ({pd.Confidence:0.00})";
                default:
                    return "";
            }
        }

        private static void PrintStateTrail(WinjayStateEngine engine, WinjayEventBus bus)
        {
            Console.WriteLine();
            Console.WriteLine("  --------------------------------------------------------------");
            Console.WriteLine("   STATE TRAIL (the user watches state, not chat)");
            Console.WriteLine("  --------------------------------------------------------------");

            foreach (var entity in engine.Entities.Values.OrderBy(e => e.Id))
            {
                if (entity is Hypothesis h)
                {
                    PrintHypothesis(h, bus);
                }
                else if (entity is Evidence e)
                {
                    Console.WriteLine($"   {e.Id}  EVIDENCE  \"{e.Content}\"  conf {e.Confidence:0.00}  src {e.Source}");
                }
                else if (entity is Conflict c)
                {
                    Console.WriteLine($"   {c.Id}  CONFLICT  {c.EvidenceId} contradicts {c.HypothesisId}");
                }
                else if (entity is Decision d)
                {
                    Console.WriteLine($"   {d.Id}  DECISION  {d.Verdict} on {d.HypothesisId} (conf {d.Confidence:0.00})");
                    Console.WriteLine($"         reason: {d.Reason}");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"   Total events in journal: {bus.Journal.Count}");
        }

        private static void PrintHypothesis(Hypothesis h, WinjayEventBus bus)
        {
            Console.WriteLine($"   {h.Id}  HYPOTHESIS  \"{h.Statement}\"");
            Console.WriteLine($"         final state: {h.State}  conf {h.Confidence:0.00}  v{h.Version}");
            Console.WriteLine($"         journey:");

            var transitions = bus.Journal
                .Where(e => e.EntityId == h.Id && e.Type == WinjayEventType.StateChanged && e.Delta != null)
                .Select(e => e.Delta)
                .ToList();

            string initialConfidence = null;
            foreach (var delta in transitions)
            {
                foreach (var change in delta.Changes)
                {
                    if (change.Field == "confidence" && initialConfidence == null)
                        initialConfidence = change.From;
                }
            }

            var startState = transitions.Count > 0
                ? transitions[0].Changes.First(c => c.Field == "state").From
                : h.State.ToString();
            var startConf = initialConfidence ?? h.Confidence.ToString("0.00");

            Console.WriteLine($"           {startState,-13} conf {startConf}  v1");

            foreach (var delta in transitions)
            {
                var stateChange = delta.Changes.FirstOrDefault(c => c.Field == "state");
                var confidenceChange = delta.Changes.FirstOrDefault(c => c.Field == "confidence");
                var line = $"             -> {stateChange.To}";
                if (confidenceChange != null) line += $"  conf {confidenceChange.To}";
                line += $"  (v{delta.ToVersion})";
                Console.WriteLine(line);
            }

            if (h.SupportingEvidence.Count > 0)
                Console.WriteLine($"         supporting: {string.Join(", ", h.SupportingEvidence)}");
            if (h.ContradictingEvidence.Count > 0)
                Console.WriteLine($"         contradicting: {string.Join(", ", h.ContradictingEvidence)}");
        }

        private static void PrintDeltaSummary(DeltaJournal journal)
        {
            Console.WriteLine("  --------------------------------------------------------------");
            Console.WriteLine("   DELTA JOURNAL (changes only, not full state)");
            Console.WriteLine("  --------------------------------------------------------------");
            foreach (var delta in journal.Deltas)
            {
                var changes = string.Join(", ", delta.Changes.Select(c => $"{c.Field}: {c.From}->{c.To}"));
                Console.WriteLine($"   {delta.EntityId} v{delta.FromVersion}->v{delta.ToVersion}  {changes}");
            }
            Console.WriteLine();
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = previous;
        }

        // ------------------------------------------------------------ mind mode

        private static int MindShow(bool stepMode)
        {
            var story = DemoMindScenario.CreateStory();
            var printedSteps = 0;
            var printedEvents = 0L;

            Console.WriteLine();
            Console.WriteLine("  ==============================================================");
            Console.WriteLine("   WINJAY XR — MIND MAP: a living map where models argue");
            Console.WriteLine("   Topic: " + DemoMindScenario.TopicLabel + " (prepared)");
            Console.WriteLine("  --------------------------------------------------------------");
            Console.WriteLine("   Explorer grows branches · Connector finds distant links");
            Console.WriteLine("   Challenger marks collisions · You curate (prune/lock/speak)");
            Console.WriteLine("  ==============================================================");
            Console.WriteLine();
            if (stepMode)
            {
                Console.WriteLine("  --step: press Enter for the next step, 'q' to quit --\n");
            }

            while (story.Step())
            {
                PrintNewStorySteps(story, ref printedSteps, ref printedEvents);
                if (stepMode)
                {
                    var key = Console.ReadLine();
                    if (key != null && key.Trim().ToLowerInvariant() == "q") break;
                }
                else
                {
                    Thread.Sleep(120);
                }
            }

            PrintNewStorySteps(story, ref printedSteps, ref printedEvents);
            PrintMindSummary(story);
            return 0;
        }

        private static void PrintNewStorySteps(DemoMindStory story, ref int printedSteps, ref long printedEvents)
        {
            while (printedSteps < story.Steps.Count)
            {
                var step = story.Steps[printedSteps++];
                var actorColor = ActorColor(step.Actor);
                var note = step.Note ?? step.Action.GetType().Name;
                var status = step.Result != null && step.Result.Ok ? "" : "  REJECTED (" + step.Result?.ErrorCode + ")";
                Console.Write($"  [{step.Tick:000}] ");
                WriteColored($"{step.Actor,-11}", actorColor);
                Console.Write($" {note,-58}");
                if (status.Length > 0) WriteColored(status, ConsoleColor.Red);
                Console.WriteLine();
            }

            foreach (var evt in story.Engine.Journal.ToList())
            {
                if (evt.Sequence <= printedEvents) continue;
                printedEvents = evt.Sequence;
                // Derived events only — the real recomputation behind the ripples.
                switch (evt.Type)
                {
                    case MindEventType.StrengthChanged:
                        Console.Write("            ");
                        WriteColored("ripple", ConsoleColor.DarkGray);
                        Console.WriteLine($"  strength of {evt.NodeId}: {evt.OldStrength:0.00} -> {evt.NewStrength:0.00}");
                        break;
                    case MindEventType.LinkDeactivated:
                        Console.Write("            ");
                        WriteColored("ripple", ConsoleColor.DarkGray);
                        Console.WriteLine($"  link {evt.LinkId} deactivated ({evt.FromId} x {evt.ToId})");
                        break;
                    case MindEventType.LinkRewired:
                        Console.Write("            ");
                        WriteColored("ripple", ConsoleColor.DarkGray);
                        Console.WriteLine($"  link {evt.LinkId} rewired: {evt.FromId} -> {evt.ToId}");
                        break;
                }
            }
        }

        private static ConsoleColor ActorColor(string actor)
        {
            switch (actor)
            {
                case "Explorer": return ConsoleColor.Cyan;
                case "Connector": return ConsoleColor.Green;
                case "Challenger": return ConsoleColor.Red;
                case "Human": return ConsoleColor.Yellow;
                default: return ConsoleColor.Gray;
            }
        }

        private static void PrintMindSummary(DemoMindStory story)
        {
            var engine = story.Engine;
            var alive = engine.Nodes.Values.Where(n => !n.Pruned).ToList();
            var activeLinks = engine.Links.Values.Count(l => l.Active);

            Console.WriteLine();
            Console.WriteLine("  --------------------------------------------------------------");
            Console.WriteLine("   FINAL MAP");
            Console.WriteLine("  --------------------------------------------------------------");
            Console.WriteLine($"   nodes: {alive.Count} alive of {engine.Nodes.Count}   links: {activeLinks} active of {engine.Links.Count}");

            var locked = alive.Where(n => n.Locked).ToList();
            if (locked.Count > 0)
                Console.WriteLine("   locked (gold): " + string.Join(", ", locked.Select(n => n.Label)));

            var ghosts = engine.Nodes.Values.Where(n => n.Ghost).ToList();
            Console.WriteLine(ghosts.Count > 0
                ? "   ghost suggestions still floating: " + string.Join(", ", ghosts.Select(n => n.Label))
                : "   ghost suggestions: resolved");

            foreach (var phase in new[] { "Day 1", "Day 2", "Day 3" })
            {
                var nodes = alive.Where(n => n.Phase == phase).Select(n => n.Label).ToList();
                if (nodes.Count > 0)
                    Console.WriteLine($"   {phase}: " + string.Join(", ", nodes));
            }

            var unresolved = alive
                .Where(n => n.Phase == null && n.Kind != MindNodeKind.Root && n.Kind != MindNodeKind.Branch)
                .OrderBy(n => n.Strength)
                .ThenBy(n => n.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (unresolved != null)
                Console.WriteLine($"   unresolved (blinks softly in XR): {unresolved.Label}  strength {unresolved.Strength:0.00}");

            Console.WriteLine();
            Console.WriteLine($"   journal: {engine.Journal.Count} events — every pixel above comes from this log");
            Console.WriteLine($"   settled: {story.Settled} — {story.SettledReason}");
            Console.WriteLine();
        }
    }
}
