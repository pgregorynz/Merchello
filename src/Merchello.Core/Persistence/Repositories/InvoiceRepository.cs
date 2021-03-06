﻿namespace Merchello.Core.Persistence.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Merchello.Core.Models;
    using Merchello.Core.Models.EntityBase;
    using Merchello.Core.Models.Rdbms;
    using Merchello.Core.Persistence.Factories;
    using Merchello.Core.Persistence.Querying;
    using Merchello.Core.Persistence.UnitOfWork;

    using Umbraco.Core;
    using Umbraco.Core.Cache;
    using Umbraco.Core.Logging;
    using Umbraco.Core.Persistence;
    using Umbraco.Core.Persistence.Querying;
    using Umbraco.Core.Persistence.SqlSyntax;

    /// <summary>
    /// Represents the Invoice Repository
    /// </summary>
    internal class InvoiceRepository : PagedRepositoryBase<IInvoice, InvoiceDto>, IInvoiceRepository
    {
        /// <summary>
        /// The invoice line item repository.
        /// </summary>
        private readonly IInvoiceLineItemRepository _invoiceLineItemRepository;

        /// <summary>
        /// The order repository.
        /// </summary>
        private readonly IOrderRepository _orderRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvoiceRepository"/> class.
        /// </summary>
        /// <param name="work">
        /// The work.
        /// </param>
        /// <param name="cache">
        /// The cache.
        /// </param>
        /// <param name="invoiceLineItemRepository">
        /// The invoice line item repository.
        /// </param>
        /// <param name="orderRepository">
        /// The order repository.
        /// </param>
        /// <param name="logger">
        /// The logger.
        /// </param>
        /// <param name="sqlSyntax">
        /// The SQL Syntax
        /// </param>
        public InvoiceRepository(IDatabaseUnitOfWork work, IRuntimeCacheProvider cache, IInvoiceLineItemRepository invoiceLineItemRepository, IOrderRepository orderRepository, ILogger logger, ISqlSyntaxProvider sqlSyntax) 
            : base(work, cache, logger, sqlSyntax)
        {
            Mandate.ParameterNotNull(invoiceLineItemRepository, "lineItemRepository");
            Mandate.ParameterNotNull(orderRepository, "orderRepository");

            _invoiceLineItemRepository = invoiceLineItemRepository;
            _orderRepository = orderRepository;
        }

        /// <summary>
        /// Performs the default search by term
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public override Page<Guid> SearchKeys(string searchTerm, long page, long itemsPerPage, string orderExpression, SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(searchTerm);

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Performs a search by term and a date rang
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="startDate">
        /// The start date.
        /// </param>
        /// <param name="endDate">
        /// The end date.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> SearchKeys(string searchTerm, DateTime startDate, DateTime endDate, long page, long itemsPerPage, string orderExpression, SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(searchTerm);
            sql.Where("invoiceDate BETWEEN @start AND @end", new { @start = startDate, @end = endDate });
            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// The get max document number.
        /// </summary>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int GetMaxDocumentNumber()
        {
            var value = Database.ExecuteScalar<object>("SELECT TOP 1 invoiceNumber FROM merchInvoice ORDER BY invoiceNumber DESC");
            return value == null ? 0 : int.Parse(value.ToString());
        }

        #region Static Collections

        /// <summary>
        /// Returns a value indicating whether or not the invoice exists in a collection.
        /// </summary>
        /// <param name="entityKey">
        /// The invoice key.
        /// </param>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool ExistsInCollection(Guid entityKey, Guid collectionKey)
        {
            var sql = new Sql();
            sql.Append("SELECT COUNT(*)")
                .Append("FROM [merchInvoice2EntityCollection]")
                .Append(
                    "WHERE [merchInvoice2EntityCollection].[invoiceKey] = @ikey AND [merchInvoice2EntityCollection].[entityCollectionKey] = @eckey",
                    new { @ikey = entityKey, @eckey = collectionKey });

            return Database.ExecuteScalar<int>(sql) > 0;
        }

        /// <summary>
        /// Adds a invoice to a static invoice collection.
        /// </summary>
        /// <param name="entityKey">
        /// The invoice key.
        /// </param>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        public void AddToCollection(Guid entityKey, Guid collectionKey)
        {
            if (this.ExistsInCollection(entityKey, collectionKey)) return;

            var dto = new Invoice2EntityCollectionDto()
            {
                InvoiceKey = entityKey,
                EntityCollectionKey = collectionKey,
                CreateDate = DateTime.Now,
                UpdateDate = DateTime.Now
            };

            Database.Insert(dto);
        }

        /// <summary>
        /// The remove invoice from collection.
        /// </summary>
        /// <param name="entityKey">
        /// The invoice key.
        /// </param>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        public void RemoveFromCollection(Guid entityKey, Guid collectionKey)
        {
            Database.Execute(
                "DELETE [merchInvoice2EntityCollection] WHERE [merchInvoice2EntityCollection].[invoiceKey] = @ikey AND [merchInvoice2EntityCollection].[entityCollectionKey] = @eckey",
                new { @ikey = entityKey, @eckey = collectionKey });
        }


        /// <summary>
        /// The get invoice keys from collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{T}"/>.
        /// </returns>
        public Page<Guid> GetKeysFromCollection(
            Guid collectionKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = new Sql();
            sql.Append("SELECT *")
              .Append("FROM [merchInvoice]")
               .Append("WHERE [merchInvoice].[pk] IN (")
               .Append("SELECT DISTINCT([invoiceKey])")
               .Append("FROM [merchInvoice2EntityCollection]")
               .Append("WHERE [merchInvoice2EntityCollection].[entityCollectionKey] = @eckey", new { @eckey = collectionKey })
               .Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// The get keys from collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <param name="term">
        /// The term.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetKeysFromCollection(
            Guid collectionKey,
            string term,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(term);
            sql.Append("AND [merchInvoice].[pk] IN (")
                .Append("SELECT DISTINCT([invoiceKey])")
                .Append("FROM [merchInvoice2EntityCollection]")
                .Append(
                    "WHERE [merchInvoice2EntityCollection].[entityCollectionKey] = @eckey",
                    new { @eckey = collectionKey })
                .Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }        

        /// <summary>
        /// The get keys not in collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetKeysNotInCollection(
            Guid collectionKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = new Sql();
            sql.Append("SELECT *")
              .Append("FROM [merchInvoice]")
               .Append("WHERE [merchInvoice].[pk] NOT IN (")
               .Append("SELECT DISTINCT([invoiceKey])")
               .Append("FROM [merchInvoice2EntityCollection]")
               .Append("WHERE [merchInvoice2EntityCollection].[entityCollectionKey] = @eckey", new { @eckey = collectionKey })
               .Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// The get keys not in collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <param name="term">
        /// The term.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetKeysNotInCollection(
            Guid collectionKey,
            string term,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(term);
            sql.Append("AND [merchInvoice].[pk] NOT IN (")
                .Append("SELECT DISTINCT([invoiceKey])")
                .Append("FROM [merchInvoice2EntityCollection]")
                .Append(
                    "WHERE [merchInvoice2EntityCollection].[entityCollectionKey] = @eckey",
                    new { @eckey = collectionKey })
                .Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Gets invoices from collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{IInvoice}"/>.
        /// </returns>
        public Page<IInvoice> GetFromCollection(
            Guid collectionKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var p = this.GetKeysFromCollection(collectionKey, page, itemsPerPage, orderExpression, sortDirection);

            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        /// <summary>
        /// The get from collection.
        /// </summary>
        /// <param name="collectionKey">
        /// The collection key.
        /// </param>
        /// <param name="term">
        /// The term.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{IInvoice}"/>.
        /// </returns>
        public Page<IInvoice> GetFromCollection(
            Guid collectionKey,
            string term,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var p = this.GetKeysFromCollection(collectionKey, term, page, itemsPerPage, orderExpression, sortDirection);

            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        #endregion

        #region Filter Queries

        /// <summary>
        /// Gets invoices matching the search term and the invoice status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="invoiceStatusKey">
        /// The invoice status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{IInvoice}"/>.
        /// </returns>
        public Page<IInvoice> GetInvoicesMatchingInvoiceStatus(
           string searchTerm,
           Guid invoiceStatusKey,
           long page,
           long itemsPerPage,
           string orderExpression,
           SortDirection sortDirection = SortDirection.Descending)
        {
            var p = this.GetInvoiceKeysMatchingInvoiceStatus(
                searchTerm,
                invoiceStatusKey,
                page,
                itemsPerPage,
                orderExpression,
                sortDirection);


            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        /// <summary>
        /// Gets invoice keys matching the search term and the invoice status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="invoiceStatusKey">
        /// The invoice status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetInvoiceKeysMatchingInvoiceStatus(
            string searchTerm,
            Guid invoiceStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(searchTerm);
            sql.Append("AND [merchInvoice].[invoiceStatusKey] = @invsk", new { @invsk = invoiceStatusKey });
            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Gets invoices matching the search term but not the invoice status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="invoiceStatusKey">
        /// The invoice status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{IInvoice}"/>.
        /// </returns>
        public Page<IInvoice> GetInvoicesMatchingTermNotInvoiceStatus(
            string searchTerm,
            Guid invoiceStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var p = this.GetInvoiceKeysMatchingTermNotInvoiceStatus(
                searchTerm,
                invoiceStatusKey,
                page,
                itemsPerPage,
                orderExpression,
                sortDirection);


            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        /// <summary>
        /// Gets invoice keys matching the search term but not the invoice status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="invoiceStatusKey">
        /// The invoice status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetInvoiceKeysMatchingTermNotInvoiceStatus(
            string searchTerm,
            Guid invoiceStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(searchTerm);
            sql.Append("AND [merchInvoice].[invoiceStatusKey] != @invsk", new { @invsk = invoiceStatusKey });
            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Gets invoices matching the search term and the order status key.
        /// </summary>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{IInvoice}"/>.
        /// </returns>
        public Page<IInvoice> GetInvoicesMatchingOrderStatus(
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var p = GetInvoiceKeysMatchingOrderStatus(
                orderStatusKey,
                page,
                itemsPerPage,
                orderExpression,
                sortDirection);

            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        /// <summary>
        /// Gets invoice keys matching the search term and the order status key.
        /// </summary>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetInvoiceKeysMatchingOrderStatus(
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = new Sql();
            sql.Append("SELECT *")
              .Append("FROM [merchInvoice]")
              .Append("WHERE [merchInvoice].[pk] IN (")
              .Append("SELECT DISTINCT(invoiceKey)")
              .Append("FROM [merchOrder]")
              .Append("WHERE [merchOrder].[orderStatusKey] = @osk", new { @osk = orderStatusKey })
              .Append(")");

            if (orderStatusKey.Equals(Core.Constants.DefaultKeys.OrderStatus.NotFulfilled))
            {
                sql.Append("OR [merchInvoice].[pk] NOT IN (");
                sql.Append("SELECT DISTINCT(invoiceKey)");
                sql.Append("FROM [merchOrder]");
                sql.Append(")");
            }

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Gets invoices matching the search term and the order status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<IInvoice> GetInvoicesMatchingOrderStatus(
            string searchTerm,
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var p = GetInvoiceKeysMatchingOrderStatus(
               orderStatusKey,
               page,
               itemsPerPage,
               orderExpression,
               sortDirection);

            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        /// <summary>
        /// Gets invoice keys matching the search term and the order status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetInvoiceKeysMatchingOrderStatus(
            string searchTerm,
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(searchTerm);
            sql.Append("AND ([merchInvoice].[pk] IN (");
            sql.Append("SELECT DISTINCT(invoiceKey)");
            sql.Append("FROM [merchOrder]");
            sql.Append("WHERE [merchOrder].[orderStatusKey] = @osk", new { @osk = orderStatusKey });
            sql.Append(")");

            if (orderStatusKey.Equals(Core.Constants.DefaultKeys.OrderStatus.NotFulfilled))
            {
                sql.Append("OR [merchInvoice].[pk] NOT IN (");
                sql.Append("SELECT DISTINCT(invoiceKey)");
                sql.Append("FROM [merchOrder]");
                sql.Append(")");
            }
            sql.Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Gets invoices matching the search term but not the order status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{IInvoice}"/>.
        /// </returns>
        public Page<IInvoice> GetInvoicesMatchingTermNotOrderStatus(
            string searchTerm,
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var p = GetInvoiceKeysMatchingOrderStatus(
              orderStatusKey,
              page,
              itemsPerPage,
              orderExpression,
              sortDirection);

            return new Page<IInvoice>()
            {
                CurrentPage = p.CurrentPage,
                ItemsPerPage = p.ItemsPerPage,
                TotalItems = p.TotalItems,
                TotalPages = p.TotalPages,
                Items = p.Items.Select(Get).ToList()
            };
        }

        /// <summary>
        /// Gets invoice keys matching the search term but not the order status key.
        /// </summary>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetInvoiceKeysMatchingTermNotOrderStatus(
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = new Sql();
            sql.Append("SELECT *")
              .Append("FROM [merchInvoice]")
              .Append("WHERE [merchInvoice].[pk] NOT IN (")
              .Append("SELECT DISTINCT(invoiceKey)")
              .Append("FROM [merchOrder]")
              .Append("WHERE [merchOrder].[orderStatusKey] != @osk", new { @osk = orderStatusKey })
              .Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        /// <summary>
        /// Gets invoice keys matching the search term but not the order status key.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <param name="orderStatusKey">
        /// The order status key.
        /// </param>
        /// <param name="page">
        /// The page.
        /// </param>
        /// <param name="itemsPerPage">
        /// The items per page.
        /// </param>
        /// <param name="orderExpression">
        /// The order expression.
        /// </param>
        /// <param name="sortDirection">
        /// The sort direction.
        /// </param>
        /// <returns>
        /// The <see cref="Page{Guid}"/>.
        /// </returns>
        public Page<Guid> GetInvoiceKeysMatchingTermNotOrderStatus(
            string searchTerm,
            Guid orderStatusKey,
            long page,
            long itemsPerPage,
            string orderExpression,
            SortDirection sortDirection = SortDirection.Descending)
        {
            var sql = BuildInvoiceSearchSql(searchTerm);
            sql.Append("AND [merchInvoice].[pk] NOT IN (");
            sql.Append("SELECT DISTINCT(invoiceKey)");
            sql.Append("FROM [merchOrder]");
            sql.Append("WHERE [merchOrder].[orderStatusKey] != @osk", new { @osk = orderStatusKey });
            sql.Append(")");

            return GetPagedKeys(page, itemsPerPage, sql, orderExpression, sortDirection);
        }

        #endregion

        /// <summary>
        /// Gets an <see cref="IInvoice"/>.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <returns>
        /// The <see cref="IInvoice"/>.
        /// </returns>
        protected override IInvoice PerformGet(Guid key)
        {
            var sql = GetBaseQuery(false)
              .Where(GetBaseWhereClause(), new { Key = key });

            var dto = Database.Fetch<InvoiceDto, InvoiceIndexDto, InvoiceStatusDto>(sql).FirstOrDefault();

            if (dto == null)
                return null;
            
            var lineItems = GetLineItemCollection(key);
            var orders = GetOrderCollection(key);
            var factory = new InvoiceFactory(lineItems, orders);
            return factory.BuildEntity(dto);
        }

        /// <summary>
        /// Gets a collection of <see cref="IInvoice"/>.
        /// </summary>
        /// <param name="keys">
        /// The keys.
        /// </param>
        /// <returns>
        /// The collection of <see cref="IInvoice"/>.
        /// </returns>
        protected override IEnumerable<IInvoice> PerformGetAll(params Guid[] keys)
        {
            if (keys.Any())
            {
                foreach (var key in keys)
                {
                    yield return Get(key);
                }
            }
            else
            {
                var dtos = Database.Fetch<InvoiceDto, InvoiceIndexDto, InvoiceStatusDto>(GetBaseQuery(false));
                foreach (var dto in dtos)
                {
                    yield return Get(dto.Key);
                }
            }
        }

        /// <summary>
        /// Gets a collection of <see cref="IInvoice"/> by query.
        /// </summary>
        /// <param name="query">
        /// The query.
        /// </param>
        /// <returns>
        /// The collection of <see cref="IInvoice"/>.
        /// </returns>
        protected override IEnumerable<IInvoice> PerformGetByQuery(IQuery<IInvoice> query)
        {
            var sqlClause = GetBaseQuery(false);
            var translator = new SqlTranslator<IInvoice>(sqlClause, query);
            var sql = translator.Translate();

            var dtos = Database.Fetch<InvoiceDto, InvoiceIndexDto, InvoiceStatusDto>(sql);

            return dtos.DistinctBy(x => x.Key).Select(dto => Get(dto.Key));
        }

        /// <summary>
        /// The get base query.
        /// </summary>
        /// <param name="isCount">
        /// The is count.
        /// </param>
        /// <returns>
        /// The <see cref="Sql"/>.
        /// </returns>
        protected override Sql GetBaseQuery(bool isCount)
        {
            var sql = new Sql();
            sql.Select(isCount ? "COUNT(*)" : "*")
               .From<InvoiceDto>(SqlSyntax)
               .InnerJoin<InvoiceIndexDto>(SqlSyntax)
               .On<InvoiceDto, InvoiceIndexDto>(SqlSyntax, left => left.Key, right => right.InvoiceKey)
               .InnerJoin<InvoiceStatusDto>(SqlSyntax)
               .On<InvoiceDto, InvoiceStatusDto>(SqlSyntax, left => left.InvoiceStatusKey, right => right.Key);

            return sql;
        }

        /// <summary>
        /// The get base where clause.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        protected override string GetBaseWhereClause()
        {
            return "merchInvoice.pk = @Key";
        }

        /// <summary>
        /// The get delete clauses.
        /// </summary>
        /// <returns>
        /// The <see cref="IEnumerable{String}"/>.
        /// </returns>
        protected override IEnumerable<string> GetDeleteClauses()
        {
            var list = new List<string>
            {
                "DELETE FROM merchAppliedPayment WHERE invoiceKey = @Key",
                "DELETE FROM merchInvoiceItem WHERE invoiceKey = @Key",
                "DELETE FROM merchInvoiceIndex WHERE invoiceKey = @Key",
                "DELETE FROM merchOfferRedeemed WHERE invoiceKey = @Key",
                "DELETE FROM merchInvoice2EntityCollection WHERE invoiceKey = @Key",
                "DELETE FROM merchInvoice WHERE pk = @Key"
            };

            return list;
        }

        /// <summary>
        /// The persist new item.
        /// </summary>
        /// <param name="entity">
        /// The entity.
        /// </param>
        protected override void PersistNewItem(IInvoice entity)
        {
            ((Entity)entity).AddingEntity();

            var factory = new InvoiceFactory(entity.Items, new OrderCollection());
            var dto = factory.BuildDto(entity);

            Database.Insert(dto);
            entity.Key = dto.Key;

            Database.Insert(dto.InvoiceIndexDto);
            ((Invoice)entity).ExamineId = dto.InvoiceIndexDto.Id;

            _invoiceLineItemRepository.SaveLineItem(entity.Items, entity.Key);

            entity.ResetDirtyProperties();
        }

        /// <summary>
        /// The persist updated item.
        /// </summary>
        /// <param name="entity">
        /// The entity.
        /// </param>
        protected override void PersistUpdatedItem(IInvoice entity)
        {
            ((Entity)entity).UpdatingEntity();

            var factory = new InvoiceFactory(entity.Items, entity.Orders);
            var dto = factory.BuildDto(entity);

            Database.Update(dto);

            _invoiceLineItemRepository.SaveLineItem(entity.Items, entity.Key);

            entity.ResetDirtyProperties();
        }

        /// <summary>
        /// Builds an invoice search query.
        /// </summary>
        /// <param name="searchTerm">
        /// The search term.
        /// </param>
        /// <returns>
        /// The <see cref="Sql"/>.
        /// </returns>
        private Sql BuildInvoiceSearchSql(string searchTerm)
        {
            searchTerm = searchTerm.Replace(",", " ");
            var invidualTerms = searchTerm.Split(' ');

            var numbers = new List<int>();
            var terms = new List<string>();

            foreach (var term in invidualTerms.Where(x => !string.IsNullOrEmpty(x)))
            {
                int invoiceNumber;
                if (int.TryParse(term, out invoiceNumber))
                {
                    numbers.Add(invoiceNumber);
                }
                else
                {
                    terms.Add(term);
                }
            }


            var sql = new Sql();

            sql.Select("*").From<InvoiceDto>(SqlSyntax);

            if (numbers.Any() && terms.Any())
            {
                sql.Where("billToName LIKE @term OR billToEmail LIKE @email OR invoiceNumber IN (@invNo)", new { @term = string.Format("%{0}%", string.Join("% ", terms)).Trim(), @email = string.Format("%{0}%", string.Join("% ", terms)).Trim(), @invNo = numbers.ToArray() });
            }
            else if (numbers.Any())
            {
                sql.Where("invoiceNumber IN (@invNo)", new { @invNo = numbers.ToArray() });
            }
            else
            {
                sql.Where("billToName LIKE @term OR billToEmail LIKE @term", new { @term = string.Format("%{0}%", string.Join("% ", terms)).Trim(), @email = string.Format("%{0}%", string.Join("% ", terms)).Trim() });
            }

            return sql;
        }

        /// <summary>
        /// The get line item collection.
        /// </summary>
        /// <param name="invoiceKey">
        /// The invoice key.
        /// </param>
        /// <returns>
        /// The <see cref="LineItemCollection"/>.
        /// </returns>
        private LineItemCollection GetLineItemCollection(Guid invoiceKey)
        {
            var sql = new Sql();
            sql.Select("*")
                .From<InvoiceItemDto>(SqlSyntax)
                .Where<InvoiceItemDto>(x => x.ContainerKey == invoiceKey);

            var dtos = Database.Fetch<InvoiceItemDto>(sql);

            var factory = new LineItemFactory();
            var collection = new LineItemCollection();
            foreach (var dto in dtos)
            {
                collection.Add(factory.BuildEntity(dto));
            }

            return collection;
        }

        /// <summary>
        /// The get order collection.
        /// </summary>
        /// <param name="invoiceKey">
        /// The invoice key.
        /// </param>
        /// <returns>
        /// The <see cref="OrderCollection"/>.
        /// </returns>
        private OrderCollection GetOrderCollection(Guid invoiceKey)
        {
            var query = Querying.Query<IOrder>.Builder.Where(x => x.InvoiceKey == invoiceKey);
            var orders = _orderRepository.GetByQuery(query);
            var collection = new OrderCollection();

            foreach (var order in orders)
            {
                collection.Add(order);
            }

            return collection;            
        }       
    }
}