﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.Data.Entity.ChangeTracking.Internal;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query.Internal;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.Data.Entity.Query.ExpressionVisitors.Internal
{
    public class InMemoryEntityQueryableExpressionVisitor : EntityQueryableExpressionVisitor
    {
        private readonly IModel _model;
        private readonly IKeyValueFactorySource _keyValueFactorySource;
        private readonly IMaterializerFactory _materializerFactory;

        private IQuerySource _querySource;

        public InMemoryEntityQueryableExpressionVisitor(
            [NotNull] IModel model,
            [NotNull] IKeyValueFactorySource keyValueFactorySource,
            [NotNull] IMaterializerFactory materializerFactory,
            [NotNull] EntityQueryModelVisitor entityQueryModelVisitor,
            [NotNull] IQuerySource querySource)
            : base(Check.NotNull(entityQueryModelVisitor, nameof(entityQueryModelVisitor)))
        {
            Check.NotNull(model, nameof(model));
            Check.NotNull(keyValueFactorySource, nameof(keyValueFactorySource));
            Check.NotNull(materializerFactory, nameof(materializerFactory));
            Check.NotNull(querySource, nameof(querySource));

            _model = model;
            _keyValueFactorySource = keyValueFactorySource;
            _materializerFactory = materializerFactory;
            _querySource = querySource;
        }

        private new InMemoryQueryModelVisitor QueryModelVisitor => (InMemoryQueryModelVisitor)base.QueryModelVisitor;

        protected override Expression VisitEntityQueryable(Type elementType)
        {
            Check.NotNull(elementType, nameof(elementType));

            var entityType = _model.GetEntityType(elementType);

            var keyProperties
                = entityType.GetPrimaryKey().Properties;

            var keyFactory = _keyValueFactorySource.GetKeyFactory(entityType.GetPrimaryKey());

            Func<ValueBuffer, IKeyValue> keyValueFactory
                = vr => keyFactory.Create(keyProperties, vr);

            if (QueryModelVisitor.QueryCompilationContext
                .QuerySourceRequiresMaterialization(_querySource))
            {
                var materializer = _materializerFactory.CreateMaterializer(entityType);

                return Expression.Call(
                    InMemoryQueryModelVisitor.EntityQueryMethodInfo.MakeGenericMethod(elementType),
                    EntityQueryModelVisitor.QueryContextParameter,
                    Expression.Constant(entityType),
                    Expression.Constant(keyValueFactory),
                    materializer,
                    Expression.Constant(QueryModelVisitor.QuerySourceRequiresTracking(_querySource)));
            }

            return Expression.Call(
                InMemoryQueryModelVisitor.ProjectionQueryMethodInfo,
                EntityQueryModelVisitor.QueryContextParameter,
                Expression.Constant(entityType));
        }
    }
}