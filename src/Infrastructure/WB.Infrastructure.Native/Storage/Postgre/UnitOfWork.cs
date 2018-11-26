﻿using System;
using System.Diagnostics;
using System.Threading;
using NHibernate;
using NHibernate.Impl;
using WB.Core.GenericSubdomains.Portable.Services;

namespace WB.Infrastructure.Native.Storage.Postgre
{
    [DebuggerDisplay("Id = {SessionId}")]
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly ISessionFactory sessionFactory;
        private readonly ILogger logger;
        private ISession session;
        private ITransaction transaction;
        private bool isDisposed = false;
        public Guid? SessionId;

        public UnitOfWork(ISessionFactory sessionFactory, ILogger logger)
        {
            if (session != null) throw new InvalidOperationException("Unit of work already started");
            if (isDisposed == true) throw new ObjectDisposedException(nameof(UnitOfWork));
            this.sessionFactory = sessionFactory;
            this.logger = logger;
        }

        public void AcceptChanges()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(UnitOfWork));

            transaction?.Commit();
        }

        public ISession Session
        {
            get
            {
                if (isDisposed)
                {
                    logger.Info($"Error getting session. Old sessionId:{SessionId} Thread:{Thread.CurrentThread.ManagedThreadId}");
                    throw new ObjectDisposedException(nameof(UnitOfWork));
                }

                if (this.session == null)
                {
                    session = sessionFactory.OpenSession();
                    transaction = session.BeginTransaction();
                    SessionId = (session as SessionImpl)?.SessionId;
                }

                return session;
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;

            transaction?.Dispose();
            session?.Dispose();

            isDisposed = true;
        }
    }
}