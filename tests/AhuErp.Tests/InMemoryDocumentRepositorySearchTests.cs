using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Тесты универсального поиска <see cref="InMemoryDocumentRepository.Search"/>.
    /// Покрывают фильтры по тексту, направлению, статусу, периоду, корреспонденту,
    /// делу номенклатуры, исполнителю, признаку «зарегистрировано»
    /// и просрочке, а также корректность сортировки результатов.
    /// </summary>
    public class InMemoryDocumentRepositorySearchTests
    {
        private static (InMemoryDocumentRepository repo,
                        Document inc, Document outg, Document intr, Document overdue) Seed()
        {
            var repo = new InMemoryDocumentRepository();
            var inc = new Document
            {
                Title = "Письмо о реорганизации",
                Summary = "Запрос от налоговой инспекции",
                Direction = DocumentDirection.Incoming,
                Type = DocumentType.Incoming,
                Status = DocumentStatus.New,
                CreationDate = new DateTime(2025, 3, 10),
                RegistrationDate = new DateTime(2025, 3, 11),
                RegistrationNumber = "ВХ-001/2025",
                Correspondent = "ИФНС № 26",
                IncomingNumber = "001-АВ",
                Deadline = new DateTime(2099, 1, 1),
                NomenclatureCaseId = 5,
                AssignedEmployeeId = 7
            };
            var outg = new Document
            {
                Title = "Ответ ИФНС",
                Summary = "Подготовка обратного письма",
                Direction = DocumentDirection.Outgoing,
                Type = DocumentType.Office,
                Status = DocumentStatus.InProgress,
                CreationDate = new DateTime(2025, 4, 1),
                RegistrationDate = new DateTime(2025, 4, 2),
                RegistrationNumber = "ИСХ-014/2025",
                Correspondent = "ИФНС № 26",
                Deadline = new DateTime(2099, 1, 1),
                NomenclatureCaseId = 5,
                AssignedEmployeeId = 7
            };
            var intr = new Document
            {
                Title = "Распоряжение по АХУ",
                Summary = "О закупке канцтоваров",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
                Status = DocumentStatus.New,
                CreationDate = new DateTime(2024, 12, 5),
                Deadline = new DateTime(2099, 1, 1),
                NomenclatureCaseId = 9,
                AssignedEmployeeId = 11
            };
            var overdue = new Document
            {
                Title = "Старая заявка",
                Direction = DocumentDirection.Internal,
                Type = DocumentType.Internal,
                Status = DocumentStatus.New,
                CreationDate = new DateTime(2024, 1, 1),
                Deadline = new DateTime(2024, 2, 1) // в прошлом
            };
            repo.Add(inc);
            repo.Add(outg);
            repo.Add(intr);
            repo.Add(overdue);
            return (repo, inc, outg, intr, overdue);
        }

        [Fact]
        public void Search_text_matches_title_summary_regnumber_correspondent_incoming()
        {
            var (repo, inc, _, _, _) = Seed();
            var byTitle = repo.Search(new DocumentSearchFilter { Text = "реорганизации" });
            var byCorrespondent = repo.Search(new DocumentSearchFilter { Text = "ИФНС" });
            var byRegNum = repo.Search(new DocumentSearchFilter { Text = "ВХ-001" });
            var byIncoming = repo.Search(new DocumentSearchFilter { Text = "001-АВ" });

            Assert.Contains(byTitle, d => d.Id == inc.Id);
            Assert.Equal(2, byCorrespondent.Count);
            Assert.Single(byRegNum);
            Assert.Single(byIncoming);
        }

        [Fact]
        public void Search_text_is_case_insensitive_and_trims()
        {
            var (repo, _, _, _, _) = Seed();
            var lower = repo.Search(new DocumentSearchFilter { Text = "  ифнс  " });
            Assert.Equal(2, lower.Count);
        }

        [Fact]
        public void Search_filter_by_direction()
        {
            var (repo, _, outg, _, _) = Seed();
            var outgoing = repo.Search(new DocumentSearchFilter { Direction = DocumentDirection.Outgoing });
            Assert.Single(outgoing);
            Assert.Equal(outg.Id, outgoing[0].Id);
        }

        [Fact]
        public void Search_filter_by_status_in_set()
        {
            var (repo, _, _, _, _) = Seed();
            var newOnly = repo.Search(new DocumentSearchFilter
            {
                StatusIn = new[] { DocumentStatus.New }
            });
            Assert.All(newOnly, d => Assert.Equal(DocumentStatus.New, d.Status));
            Assert.Equal(3, newOnly.Count);
        }

        [Fact]
        public void Search_status_in_takes_precedence_over_single_status()
        {
            var (repo, _, _, _, _) = Seed();
            var combined = repo.Search(new DocumentSearchFilter
            {
                Status = DocumentStatus.Completed, // должно быть проигнорировано
                StatusIn = new[] { DocumentStatus.New, DocumentStatus.InProgress }
            });
            Assert.Equal(4, combined.Count);
        }

        [Fact]
        public void Search_period_uses_registration_date_when_present_else_creation()
        {
            var (repo, _, _, intr, _) = Seed();
            var dec24 = repo.Search(new DocumentSearchFilter
            {
                From = new DateTime(2024, 12, 1),
                To = new DateTime(2024, 12, 31)
            });
            Assert.Contains(dec24, d => d.Id == intr.Id);
            Assert.DoesNotContain(dec24, d => d.RegistrationDate?.Year == 2025);
        }

        [Fact]
        public void Search_filter_by_correspondent_partial_match()
        {
            var (repo, _, _, _, _) = Seed();
            var ifns = repo.Search(new DocumentSearchFilter { Correspondent = "26" });
            Assert.Equal(2, ifns.Count);
        }

        [Fact]
        public void Search_filter_by_nomenclature_case()
        {
            var (repo, _, _, _, _) = Seed();
            var case5 = repo.Search(new DocumentSearchFilter { NomenclatureCaseId = 5 });
            Assert.Equal(2, case5.Count);
        }

        [Fact]
        public void Search_filter_by_assigned_employee()
        {
            var (repo, _, _, _, _) = Seed();
            var emp7 = repo.Search(new DocumentSearchFilter { AssignedEmployeeId = 7 });
            Assert.Equal(2, emp7.Count);
        }

        [Fact]
        public void Search_registered_only_filters_unregistered()
        {
            var (repo, _, _, _, _) = Seed();
            var registered = repo.Search(new DocumentSearchFilter { RegisteredOnly = true });
            Assert.All(registered, d => Assert.False(string.IsNullOrEmpty(d.RegistrationNumber)));
            Assert.Equal(2, registered.Count);
        }

        [Fact]
        public void Search_overdue_only_returns_only_past_deadlines_for_active_docs()
        {
            var (repo, _, _, _, overdue) = Seed();
            var only = repo.Search(new DocumentSearchFilter { OverdueOnly = true });
            Assert.Single(only);
            Assert.Equal(overdue.Id, only[0].Id);
        }

        [Fact]
        public void Search_results_are_sorted_descending_by_registration_date()
        {
            var (repo, _, _, _, _) = Seed();
            var all = repo.Search(new DocumentSearchFilter());
            Assert.Equal(4, all.Count);
            // Самый поздний — ИСХ от 2025-04-02
            Assert.Equal("ИСХ-014/2025", all[0].RegistrationNumber);
        }

        [Fact]
        public void Search_compound_outgoing_text_period()
        {
            var (repo, _, outg, _, _) = Seed();
            var hits = repo.Search(new DocumentSearchFilter
            {
                Direction = DocumentDirection.Outgoing,
                Text = "ИФНС",
                From = new DateTime(2025, 4, 1),
                To = new DateTime(2025, 4, 30)
            });
            Assert.Single(hits);
            Assert.Equal(outg.Id, hits[0].Id);
        }

        [Fact]
        public void Search_null_filter_returns_all()
        {
            var (repo, _, _, _, _) = Seed();
            var all = repo.Search(null);
            Assert.Equal(4, all.Count);
        }
    }
}
