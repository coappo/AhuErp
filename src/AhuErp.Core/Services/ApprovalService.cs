using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IApprovalService"/>. Логика прохождения:
    /// <list type="bullet">
    ///   <item><description>Шаблон содержит этапы (<see cref="ApprovalStage"/>) с
    ///     порядком и флагом параллельности.</description></item>
    ///   <item><description>При запуске маршрута для документа создаются
    ///     <see cref="DocumentApproval"/> с тем же порядком.</description></item>
    ///   <item><description>Решение «Отклонено» останавливает маршрут;
    ///     все Approved → завершение маршрута.</description></item>
    /// </list>
    /// Все ключевые события идут в журнал аудита.
    /// </summary>
    public sealed class ApprovalService : IApprovalService
    {
        private readonly IApprovalRepository _repository;
        private readonly IDocumentRepository _documents;
        private readonly IAuditService _audit;
        private readonly IWorkflowService _workflow;
        private readonly ISubstitutionService _substitution;

        public ApprovalService(
            IApprovalRepository repository,
            IDocumentRepository documents,
            IAuditService audit,
            IWorkflowService workflow = null,
            ISubstitutionService substitution = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _workflow = workflow;
            _substitution = substitution;
        }

        public IReadOnlyList<ApprovalRouteTemplate> ListTemplates(bool activeOnly = true)
            => _repository.ListTemplates(activeOnly);

        public ApprovalRouteTemplate GetTemplate(int id) => _repository.GetTemplate(id);

        public ApprovalRouteTemplate AddTemplate(ApprovalRouteTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (string.IsNullOrWhiteSpace(template.Name))
                throw new ArgumentException("Имя шаблона обязательно.", nameof(template));
            return _repository.AddTemplate(template);
        }

        public ApprovalStage AddStage(int templateId, ApprovalStage stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            stage.RouteTemplateId = templateId;
            return _repository.AddStage(stage);
        }

        public IReadOnlyList<DocumentApproval> StartApproval(int documentId, int templateId, int actorId)
        {
            var doc = _documents.GetById(documentId)
                ?? throw new InvalidOperationException($"Документ #{documentId} не найден.");
            var template = _repository.GetTemplate(templateId)
                ?? throw new InvalidOperationException($"Шаблон маршрута #{templateId} не найден.");

            var stages = _repository.ListStages(templateId)
                .OrderBy(s => s.Order)
                .ToList();
            if (stages.Count == 0)
                throw new InvalidOperationException("В шаблоне маршрута нет этапов.");
            if (stages.Any(s => !s.ApproverEmployeeId.HasValue))
                throw new InvalidOperationException(
                    "Этапы шаблона должны иметь конкретного согласующего (ApproverEmployeeId).");

            var approvals = new List<DocumentApproval>();
            var now = DateTime.Now;
            foreach (var stage in stages)
            {
                // Phase 11: согласующего может замещать другой сотрудник.
                int actualApproverId = stage.ApproverEmployeeId.Value;
                if (_substitution != null)
                {
                    actualApproverId = _substitution.ResolveActualExecutor(
                        actualApproverId, now, SubstitutionScope.ApprovalsOnly);
                }

                var approval = _repository.AddApproval(new DocumentApproval
                {
                    DocumentId = doc.Id,
                    StageId = stage.Id,
                    Order = stage.Order,
                    IsParallel = stage.IsParallel,
                    ApproverId = actualApproverId,
                    Decision = ApprovalDecision.Pending
                });
                approvals.Add(approval);
            }

            doc.ApprovalStatus = ApprovalRouteStatus.InProgress;
            _documents.Update(doc);

            _audit.Record(AuditActionType.ApprovalSent, nameof(Document), doc.Id, actorId,
                newValues: $"TemplateId={templateId}; Stages={stages.Count}");

            return approvals.AsReadOnly();
        }

        public DocumentApproval ApplyDecision(int approvalId, ApprovalDecision decision, int actorId, string comment = null)
        {
            if (decision == ApprovalDecision.Pending)
                throw new ArgumentException("Решение Pending не может быть применено вручную.");

            var approval = _repository.GetApproval(approvalId)
                ?? throw new InvalidOperationException($"Этап согласования #{approvalId} не найден.");
            if (approval.Decision != ApprovalDecision.Pending)
                throw new InvalidOperationException("По этому этапу уже принято решение.");

            approval.Decision = decision;
            approval.Comment = comment;
            approval.DecisionDate = DateTime.UtcNow;
            _repository.UpdateApproval(approval);

            _audit.Record(
                decision == ApprovalDecision.Approved ? AuditActionType.ApprovalSigned
                    : decision == ApprovalDecision.Rejected ? AuditActionType.ApprovalRejected
                    : AuditActionType.Updated,
                nameof(DocumentApproval), approval.Id, actorId,
                newValues: $"Decision={decision}", details: comment);

            // Завершаем маршрут целиком, если: либо отклонено, либо все этапы
            // приняты. Замечания не блокируют маршрут — это «мягкое» решение.
            var all = _repository.ListApprovalsByDocument(approval.DocumentId);
            var doc = _documents.GetById(approval.DocumentId);
            if (decision == ApprovalDecision.Rejected)
            {
                doc.ApprovalStatus = ApprovalRouteStatus.Rejected;
                _documents.Update(doc);
            }
            else if (all.All(a => a.Decision == ApprovalDecision.Approved
                                   || a.Decision == ApprovalDecision.Comments))
            {
                doc.ApprovalStatus = ApprovalRouteStatus.Completed;
                _documents.Update(doc);
                _workflow?.OnApprovalRouteCompleted(doc.Id, actorId);
            }

            return approval;
        }

        public IReadOnlyList<DocumentApproval> ListByDocument(int documentId)
            => _repository.ListApprovalsByDocument(documentId);
    }
}
