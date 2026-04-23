using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    public class InventoryServiceTests
    {
        private readonly InMemoryInventoryRepository _repo;
        private readonly InventoryService _service;

        public InventoryServiceTests()
        {
            _repo = new InMemoryInventoryRepository();
            _service = new InventoryService(_repo);

            _repo.AddItem(new InventoryItem
            {
                Name = "Бумага А4",
                Category = InventoryCategory.Stationery,
                TotalQuantity = 10
            });
        }

        [Fact]
        public void ProcessTransaction_adds_stock_on_positive_change()
        {
            var tx = _service.ProcessTransaction(itemId: 1, quantityChange: 5, documentId: null, userId: 42);

            Assert.Equal(15, _repo.GetItem(1).TotalQuantity);
            Assert.Equal(5, tx.QuantityChanged);
            Assert.Equal(42, tx.InitiatorId);
            Assert.Null(tx.DocumentId);
            Assert.Single(_repo.ListTransactions());
        }

        [Fact]
        public void ProcessTransaction_deducts_stock_on_negative_change_with_document()
        {
            var tx = _service.ProcessTransaction(itemId: 1, quantityChange: -3, documentId: 77, userId: 42);

            Assert.Equal(7, _repo.GetItem(1).TotalQuantity);
            Assert.Equal(-3, tx.QuantityChanged);
            Assert.Equal(77, tx.DocumentId);
        }

        [Fact]
        public void ProcessTransaction_throws_when_deduction_exceeds_stock()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.ProcessTransaction(itemId: 1, quantityChange: -999, documentId: 77, userId: 42));

            Assert.Contains("Недостаточно остатка", ex.Message);
            Assert.Equal(10, _repo.GetItem(1).TotalQuantity);
            Assert.Empty(_repo.ListTransactions());
        }

        [Fact]
        public void ProcessTransaction_throws_when_deduction_has_no_document()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.ProcessTransaction(itemId: 1, quantityChange: -1, documentId: null, userId: 42));

            Assert.Contains("основании документа", ex.Message);
            Assert.Equal(10, _repo.GetItem(1).TotalQuantity);
        }

        [Fact]
        public void ProcessTransaction_throws_when_quantity_change_is_zero()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.ProcessTransaction(itemId: 1, quantityChange: 0, documentId: 77, userId: 42));
        }

        [Fact]
        public void ProcessTransaction_throws_for_missing_item()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _service.ProcessTransaction(itemId: 99, quantityChange: 5, documentId: null, userId: 42));
            Assert.Contains("не найдена", ex.Message);
        }

        [Fact]
        public void ProcessTransaction_throws_for_invalid_user()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.ProcessTransaction(itemId: 1, quantityChange: 5, documentId: null, userId: 0));
        }

        [Fact]
        public void ProcessTransaction_can_deduct_exactly_to_zero()
        {
            var tx = _service.ProcessTransaction(itemId: 1, quantityChange: -10, documentId: 77, userId: 42);

            Assert.Equal(0, _repo.GetItem(1).TotalQuantity);
            Assert.Equal(-10, tx.QuantityChanged);
        }

        [Fact]
        public void ProcessTransaction_links_transaction_to_document_id()
        {
            _service.ProcessTransaction(itemId: 1, quantityChange: -2, documentId: 123, userId: 5);
            _service.ProcessTransaction(itemId: 1, quantityChange: -1, documentId: 123, userId: 5);
            _service.ProcessTransaction(itemId: 1, quantityChange: 4, documentId: null, userId: 5);

            var tied = _repo.ListTransactions(1);
            Assert.Equal(3, tied.Count);
            Assert.Equal(2, System.Linq.Enumerable.Count(tied, t => t.DocumentId == 123));
        }
    }
}
