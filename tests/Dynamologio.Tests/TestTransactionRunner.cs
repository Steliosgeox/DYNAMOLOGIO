using System;
using Dynamologio.Core.Interfaces;

namespace Dynamologio.Tests
{
    public class TestTransactionRunner : ITransactionRunner
    {
        public void RunInTransaction(Action action)
        {
            action();
        }

        public T RunInTransaction<T>(Func<T> action)
        {
            return action();
        }
    }
}
