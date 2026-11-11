using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

namespace NetFrameWork.Database.WhereExpression
{
    /// <summary>
    /// 
    /// </summary>
    public class HashedListCache<T> : IExpressionCache<T> where T : class
    {
        private readonly Dictionary<int, SortedList<Expression, T>> _mStorage = new();
        private readonly ReaderWriterLockSlim _mRwLock = new();

        /// <summary>
        /// 
        /// </summary>
        public T Get(Expression key, Func<Expression, T> creator)
        {
            SortedList<Expression, T> sortedList;
            T value;

            int hash = new Hasher().Hash(key);
            _mRwLock.EnterReadLock();
            try
            {
                if (_mStorage.TryGetValue(hash, out sortedList) &&
                    sortedList.TryGetValue(key, out value))
                {
                    return value;
                }
            }
            finally
            {
                _mRwLock.ExitReadLock();
            }

            _mRwLock.EnterWriteLock();
            try
            {
                if (!_mStorage.TryGetValue(hash, out sortedList))
                {
                    sortedList = new SortedList<Expression, T>(new Comparer());
                    _mStorage.Add(hash, sortedList);
                }

                if (!sortedList.TryGetValue(key, out value))
                {
                    value = creator(key);
                    sortedList.Add(key, value);
                }
                
                return value;
            }
            finally
            {
                _mRwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private class Hasher : ExpressionHasher
        {
            protected override Expression VisitConstant(ConstantExpression c)
            {
                return c;
            }
        }

        internal class Comparer : ExpressionComparer
        {
            protected override int CompareConstant(ConstantExpression x, ConstantExpression y)
            {
                return 0;
            }
        }
    }
}
