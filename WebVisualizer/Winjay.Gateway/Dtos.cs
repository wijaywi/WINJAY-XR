using System.Collections.Generic;
using System.Linq;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;

namespace Winjay.Gateway
{
    public sealed class FieldChangeDto
    {
        public string Field { get; set; }
        public string From { get; set; }
        public string To { get; set; }

        public static FieldChangeDto Create(FieldChange c) => new FieldChangeDto { Field = c.Field, From = c.From, To = c.To };
    }

    public sealed class DeltaDto
    {
        public string EntityId { get; set; }
        public int FromVersion { get; set; }
        public int ToVersion { get; set; }
        public List<FieldChangeDto> Changes { get; set; }

        public static DeltaDto From(Delta d) => new DeltaDto
        {
            EntityId = d.EntityId,
            FromVersion = d.FromVersion,
            ToVersion = d.ToVersion,
            Changes = d.Changes.Select(FieldChangeDto.Create).ToList()
        };
    }

    public sealed class EventDto
    {
        public long Seq { get; set; }
        public string Type { get; set; }
        public string Actor { get; set; }
        public string EntityId { get; set; }
        public string EntityType { get; set; }
        public string Detail { get; set; }
        public DeltaDto Delta { get; set; }

        public static EventDto From(WinjayEvent e) => new EventDto
        {
            Seq = e.Sequence,
            Type = e.Type.ToString(),
            Actor = e.Actor,
            EntityId = e.EntityId,
            EntityType = e.EntityType?.ToString(),
            Detail = e.Detail,
            Delta = e.Delta != null ? DeltaDto.From(e.Delta) : null
        };
    }

    public sealed class EntityDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public int Version { get; set; }
        public double Confidence { get; set; }

        public string Content { get; set; }
        public string Source { get; set; }
        public string Statement { get; set; }
        public string State { get; set; }
        public List<string> Supporting { get; set; }
        public List<string> Contradicting { get; set; }
        public string HypothesisId { get; set; }
        public string EvidenceId { get; set; }
        public string Verdict { get; set; }
        public string Reason { get; set; }

        public static EntityDto From(WinjayEntity entity)
        {
            var dto = new EntityDto
            {
                Id = entity.Id,
                Type = entity.Type.ToString(),
                Version = entity.Version,
                Confidence = entity.Confidence
            };

            switch (entity)
            {
                case Evidence e:
                    dto.Content = e.Content;
                    dto.Source = e.Source;
                    break;
                case Hypothesis h:
                    dto.Statement = h.Statement;
                    dto.State = h.State.ToString();
                    dto.Supporting = new List<string>(h.SupportingEvidence);
                    dto.Contradicting = new List<string>(h.ContradictingEvidence);
                    break;
                case Conflict c:
                    dto.HypothesisId = c.HypothesisId;
                    dto.EvidenceId = c.EvidenceId;
                    break;
                case Decision d:
                    dto.HypothesisId = d.HypothesisId;
                    dto.Verdict = d.Verdict;
                    dto.Reason = d.Reason;
                    break;
            }
            return dto;
        }
    }

    public sealed class StepDto
    {
        public int Tick { get; set; }
        public string Agent { get; set; }
        public string Kind { get; set; }
        public string Target { get; set; }
        public string Description { get; set; }
        public bool Ok { get; set; }
        public string ErrorCode { get; set; }

        public static StepDto From(Winjay.Agents.ScenarioStep step)
        {
            var p = step.Proposal;
            return new StepDto
            {
                Tick = step.Tick,
                Agent = step.Agent,
                Kind = p.Kind.ToString(),
                Target = p.TargetId,
                Description = Describe(p),
                Ok = step.Result.Ok,
                ErrorCode = step.Result.ErrorCode
            };
        }

        private static string Describe(Winjay.Core.Proposals.AgentProposal p)
        {
            switch (p.Kind)
            {
                case Winjay.Core.Proposals.ProposalKind.CreateEvidence:
                    var ce = (Winjay.Core.Proposals.CreateEvidenceProposal)p;
                    return $"\"{ce.Content}\" (conf {ce.Confidence:0.00})";
                case Winjay.Core.Proposals.ProposalKind.CreateHypothesis:
                    var ch = (Winjay.Core.Proposals.CreateHypothesisProposal)p;
                    return $"\"{ch.Statement}\"";
                case Winjay.Core.Proposals.ProposalKind.Support:
                    var sp = (Winjay.Core.Proposals.SupportProposal)p;
                    return $"supported by {sp.EvidenceId}";
                case Winjay.Core.Proposals.ProposalKind.Contradict:
                    var cn = (Winjay.Core.Proposals.ContradictProposal)p;
                    return $"contradicted by {cn.EvidenceId}";
                case Winjay.Core.Proposals.ProposalKind.ProposeDecision:
                    var pd = (Winjay.Core.Proposals.ProposeDecisionProposal)p;
                    return $"{pd.Verdict}: {pd.Reason}";
                default:
                    return "";
            }
        }
    }
}
