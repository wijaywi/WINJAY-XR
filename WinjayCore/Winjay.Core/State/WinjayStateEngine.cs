using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.Proposals;

namespace Winjay.Core.State
{
    /// <summary>
    /// The single source of truth. Agents submit proposals; this engine
    /// validates them against the transition table, entity existence and
    /// versioning, then commits and publishes events + deltas. Deterministic:
    /// the same proposal sequence always produces the same state.
    /// </summary>
    public sealed class WinjayStateEngine
    {
        private const double MinConfidence = 0.01;
        private const double MaxConfidence = 0.99;
        private const double SupportGain = 0.35;
        private const double ContradictPenalty = 0.35;

        private readonly WinjayEventBus _bus;
        private readonly Dictionary<string, WinjayEntity> _entities = new Dictionary<string, WinjayEntity>();
        private long _sequence;
        private int _conflictCounter;
        private int _decisionCounter;

        public WinjayStateEngine(WinjayEventBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public IReadOnlyDictionary<string, WinjayEntity> Entities => _entities;

        public long Sequence => _sequence;

        public T Get<T>(string id) where T : WinjayEntity
        {
            return _entities.TryGetValue(id, out var entity) ? entity as T : null;
        }

        public Result Apply(AgentProposal proposal)
        {
            if (proposal == null) throw new ArgumentNullException(nameof(proposal));

            var proposalSequence = ++_sequence;

            switch (proposal.Kind)
            {
                case ProposalKind.CreateEvidence: return ApplyCreateEvidence((CreateEvidenceProposal)proposal, proposalSequence);
                case ProposalKind.CreateHypothesis: return ApplyCreateHypothesis((CreateHypothesisProposal)proposal, proposalSequence);
                case ProposalKind.ProposeDecision: return ApplyProposeDecision((ProposeDecisionProposal)proposal, proposalSequence);
                default: return ApplyTransition(proposal, proposalSequence);
            }
        }

        // ---------------------------------------------------------------- creation

        private Result ApplyCreateEvidence(CreateEvidenceProposal p, long seq)
        {
            if (_entities.ContainsKey(p.EvidenceId))
                return Fail(Result.Codes.DuplicateId, $"Evidence '{p.EvidenceId}' already exists.");
            if (p.Content == null || p.Content.Trim().Length == 0)
                return Fail(Result.Codes.ValidationFailed, $"Evidence '{p.EvidenceId}' has no content.");

            var evidence = new Evidence(p.EvidenceId, p.Content, p.Source, Clamp(p.Confidence));
            evidence.Record(seq, p.Actor, "CREATE_EVIDENCE");
            _entities.Add(evidence.Id, evidence);

            var events = new List<WinjayEvent>
            {
                Emit(WinjayEventType.EntityCreated, p.Actor, evidence.Id, EntityType.Evidence),
                Emit(WinjayEventType.EvidenceAdded, p.Actor, evidence.Id, EntityType.Evidence,
                    detail: $"'{p.Content}' from {p.Source} (confidence {evidence.Confidence.ToString("0.00", CultureInfo.InvariantCulture)})")
            };
            return Result.Success(events);
        }

        private Result ApplyCreateHypothesis(CreateHypothesisProposal p, long seq)
        {
            if (_entities.ContainsKey(p.HypothesisId))
                return Fail(Result.Codes.DuplicateId, $"Hypothesis '{p.HypothesisId}' already exists.");
            if (p.Statement == null || p.Statement.Trim().Length == 0)
                return Fail(Result.Codes.ValidationFailed, $"Hypothesis '{p.HypothesisId}' has no statement.");
            foreach (var evidenceId in p.BasedOnEvidence)
            {
                if (Get<Evidence>(evidenceId) == null)
                    return Fail(Result.Codes.NotFound, $"Based-on evidence '{evidenceId}' not found.");
            }

            var hypothesis = new Hypothesis(p.HypothesisId, p.Statement, Clamp(p.Confidence));
            hypothesis.Record(seq, p.Actor, "CREATE_HYPOTHESIS");
            _entities.Add(hypothesis.Id, hypothesis);

            var events = new List<WinjayEvent>
            {
                Emit(WinjayEventType.EntityCreated, p.Actor, hypothesis.Id, EntityType.Hypothesis),
                Emit(WinjayEventType.HypothesisCreated, p.Actor, hypothesis.Id, EntityType.Hypothesis,
                    detail: $"'{p.Statement}' based on [{string.Join(", ", p.BasedOnEvidence)}]")
            };
            return Result.Success(events);
        }

        private Result ApplyProposeDecision(ProposeDecisionProposal p, long seq)
        {
            if (Get<Hypothesis>(p.HypothesisId) == null)
                return Fail(Result.Codes.NotFound, $"Hypothesis '{p.HypothesisId}' not found.");
            if (CheckStaleVersion(p, Get<Hypothesis>(p.HypothesisId), out var stale))
                return stale;

            var decision = new Decision(NextDecisionId(), p.HypothesisId, p.Verdict, p.Reason, Clamp(p.Confidence));
            decision.Record(seq, p.Actor, "PROPOSE_DECISION");
            _entities.Add(decision.Id, decision);

            var events = new List<WinjayEvent>
            {
                Emit(WinjayEventType.EntityCreated, p.Actor, decision.Id, EntityType.Decision),
                Emit(WinjayEventType.DecisionProposed, p.Actor, decision.Id, EntityType.Decision,
                    detail: $"{p.Verdict}: {p.Reason}"),
                Emit(WinjayEventType.DecisionCommitted, p.Actor, decision.Id, EntityType.Decision,
                    detail: $"{p.Verdict} on {p.HypothesisId}")
            };
            return Result.Success(events);
        }

        // ---------------------------------------------------------------- transitions

        private Result ApplyTransition(AgentProposal p, long seq)
        {
            if (p.TargetId == null)
                return Fail(Result.Codes.ValidationFailed, "Proposal carries no target entity.");

            var hypothesis = Get<Hypothesis>(p.TargetId);
            if (hypothesis == null)
                return Fail(Result.Codes.NotFound, $"Hypothesis '{p.TargetId}' not found.");
            if (CheckStaleVersion(p, hypothesis, out var stale))
                return stale;
            if (!TransitionTable.TryGetTransition(hypothesis.State, p.Kind, out var next))
                return Fail(Result.Codes.InvalidTransition,
                    $"Transition {hypothesis.State} -> ({p.Kind}) is not allowed.");

            switch (p.Kind)
            {
                case ProposalKind.Support:
                    var support = (SupportProposal)p;
                    var supportEvidence = Get<Evidence>(support.EvidenceId);
                    if (supportEvidence == null)
                        return Fail(Result.Codes.NotFound, $"Evidence '{support.EvidenceId}' not found.");
                    if (hypothesis.SupportingEvidence.Contains(support.EvidenceId))
                        return Fail(Result.Codes.ValidationFailed, $"Evidence '{support.EvidenceId}' already supports '{hypothesis.Id}'.");
                    if (hypothesis.ContradictingEvidence.Contains(support.EvidenceId))
                        return Fail(Result.Codes.ValidationFailed, $"Evidence '{support.EvidenceId}' already contradicts '{hypothesis.Id}' and cannot flip.");
                    return CommitSupport(hypothesis, supportEvidence, next, seq, p.Actor);

                case ProposalKind.Contradict:
                    var contradict = (ContradictProposal)p;
                    var contradictEvidence = Get<Evidence>(contradict.EvidenceId);
                    if (contradictEvidence == null)
                        return Fail(Result.Codes.NotFound, $"Evidence '{contradict.EvidenceId}' not found.");
                    if (hypothesis.ContradictingEvidence.Contains(contradict.EvidenceId))
                        return Fail(Result.Codes.ValidationFailed, $"Evidence '{contradict.EvidenceId}' already contradicts '{hypothesis.Id}'.");
                    if (hypothesis.SupportingEvidence.Contains(contradict.EvidenceId))
                        return Fail(Result.Codes.ValidationFailed, $"Evidence '{contradict.EvidenceId}' already supports '{hypothesis.Id}' and cannot flip.");
                    return CommitContradict(hypothesis, contradictEvidence, next, seq, p.Actor);

                default:
                    return CommitStateChange(hypothesis, next, seq, p.Actor);
            }
        }

        // ---------------------------------------------------------------- commits

        private Result CommitSupport(Hypothesis h, Evidence evidence, HypothesisState next, long seq, string actor)
        {
            var fromVersion = h.Version;
            var changes = new List<FieldChange>
            {
                new FieldChange("state", h.State.ToString(), next.ToString()),
                new FieldChange("supporting_evidence", JoinIds(h.SupportingEvidence),
                    JoinIds(Concat(h.SupportingEvidence, evidence.Id)))
            };
            AppendConfidenceChange(changes, h.Confidence, ConfidenceAfterSupport(h.Confidence, evidence.Confidence));

            h.State = next;
            h.AddSupport(evidence.Id);
            h.Confidence = ConfidenceAfterSupport(h.Confidence, evidence.Confidence);
            h.Version++;
            h.Record(seq, actor, "SUPPORT");

            var events = new List<WinjayEvent>
            {
                Emit(WinjayEventType.StateChanged, actor, h.Id, EntityType.Hypothesis,
                    new Delta(h.Id, fromVersion, h.Version, changes),
                    detail: $"supported by {evidence.Id}")
            };
            return Result.Success(events);
        }

        private Result CommitContradict(Hypothesis h, Evidence evidence, HypothesisState next, long seq, string actor)
        {
            var fromVersion = h.Version;
            var changes = new List<FieldChange>
            {
                new FieldChange("state", h.State.ToString(), next.ToString()),
                new FieldChange("contradicting_evidence", JoinIds(h.ContradictingEvidence),
                    JoinIds(Concat(h.ContradictingEvidence, evidence.Id)))
            };
            AppendConfidenceChange(changes, h.Confidence, ConfidenceAfterContradict(h.Confidence, evidence.Confidence));

            h.State = next;
            h.AddContradiction(evidence.Id);
            h.Confidence = ConfidenceAfterContradict(h.Confidence, evidence.Confidence);
            h.Version++;
            h.Record(seq, actor, "CONTRADICT");

            var conflict = new Conflict(NextConflictId(), h.Id, evidence.Id, evidence.Confidence);
            conflict.Record(seq, actor, "CONTRADICT");
            _entities.Add(conflict.Id, conflict);

            var events = new List<WinjayEvent>
            {
                Emit(WinjayEventType.StateChanged, actor, h.Id, EntityType.Hypothesis,
                    new Delta(h.Id, fromVersion, h.Version, changes),
                    detail: $"contradicted by {evidence.Id}"),
                Emit(WinjayEventType.EntityCreated, actor, conflict.Id, EntityType.Conflict),
                Emit(WinjayEventType.ConflictDetected, actor, conflict.Id, EntityType.Conflict,
                    detail: $"{evidence.Id} contradicts {h.Id}")
            };
            return Result.Success(events);
        }

        private Result CommitStateChange(Hypothesis h, HypothesisState next, long seq, string actor)
        {
            var fromVersion = h.Version;
            var changes = new List<FieldChange>
            {
                new FieldChange("state", h.State.ToString(), next.ToString())
            };

            h.State = next;
            h.Version++;
            h.Record(seq, actor, ToAction(next));

            var events = new List<WinjayEvent>
            {
                Emit(WinjayEventType.StateChanged, actor, h.Id, EntityType.Hypothesis,
                    new Delta(h.Id, fromVersion, h.Version, changes),
                    detail: $"{h.Id} -> {next}")
            };

            if (next == HypothesisState.Falsifying)
            {
                events.Add(Emit(WinjayEventType.FalsificationStarted, actor, h.Id, EntityType.Hypothesis,
                    detail: $"falsification active on {h.Id}"));
            }
            return Result.Success(events);
        }

        // ---------------------------------------------------------------- helpers

        private static string ToAction(HypothesisState next)
        {
            switch (next)
            {
                case HypothesisState.Investigating: return "START_INVESTIGATION";
                case HypothesisState.Resolved: return "RESOLVE";
                case HypothesisState.Falsifying: return "START_FALSIFICATION";
                case HypothesisState.Failed: return "FAIL";
                default: return "STATE_CHANGE";
            }
        }

        private bool CheckStaleVersion(AgentProposal p, Hypothesis h, out Result failure)
        {
            if (p.ExpectedVersion >= 0 && p.ExpectedVersion != h.Version)
            {
                failure = Fail(Result.Codes.StaleVersion,
                    $"Proposal targets version {p.ExpectedVersion} but '{h.Id}' is at version {h.Version}.");
                return true;
            }
            failure = null;
            return false;
        }

        private static double ConfidenceAfterSupport(double current, double evidenceConfidence)
        {
            return Clamp(current + (1.0 - current) * evidenceConfidence * SupportGain);
        }

        private static double ConfidenceAfterContradict(double current, double evidenceConfidence)
        {
            return Clamp(current * (1.0 - evidenceConfidence * ContradictPenalty));
        }

        private static double Clamp(double value)
        {
            if (double.IsNaN(value) || value < MinConfidence) return MinConfidence;
            if (value > MaxConfidence) return MaxConfidence;
            return value;
        }

        private static void AppendConfidenceChange(List<FieldChange> changes, double from, double to)
        {
            changes.Add(new FieldChange("confidence",
                from.ToString("0.000", CultureInfo.InvariantCulture),
                to.ToString("0.000", CultureInfo.InvariantCulture)));
        }

        private static string JoinIds(IEnumerable<string> ids)
        {
            return string.Join(",", ids);
        }

        private static IEnumerable<string> Concat(IEnumerable<string> ids, string extra)
        {
            foreach (var id in ids) yield return id;
            yield return extra;
        }

        private string NextConflictId()
        {
            _conflictCounter++;
            return "CF" + _conflictCounter.ToString("D3", CultureInfo.InvariantCulture);
        }

        private string NextDecisionId()
        {
            _decisionCounter++;
            return "DC" + _decisionCounter.ToString("D3", CultureInfo.InvariantCulture);
        }

        private WinjayEvent Emit(WinjayEventType type, string actor, string entityId,
            EntityType? entityType = null, Delta delta = null, string detail = null)
        {
            var evt = new WinjayEvent(++_sequence, type, actor, entityId, entityType, delta, detail);
            _bus.Publish(evt);
            return evt;
        }

        private Result Fail(string code, string message)
        {
            var evt = Emit(WinjayEventType.ProposalRejected, "STATE_ENGINE", null, null, null, $"{code}: {message}");
            var result = Result.Failure(code, message);
            result.AttachRejectionEvent(evt);
            return result;
        }
    }
}
