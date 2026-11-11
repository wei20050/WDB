using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace NetFrameWork.Database.WhereExpression
{
    /// <summary>
    /// 
    /// </summary>
    public class FastEvaluator
    {
        private static readonly IExpressionCache<Func<List<object>, object>> SCache =
            new HashedListCache<Func<List<object>, object>>();

        private readonly IExpressionCache<Func<List<object>, object>> _mCache;
        private readonly Func<Expression, Func<List<object>, object>> _mCreatorDelegate;

        /// <summary>
        /// 
        /// </summary>
        public FastEvaluator()
            : this(SCache)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cache"></param>
        public FastEvaluator(IExpressionCache<Func<List<object>, object>> cache)
        {
            _mCache = cache;
            _mCreatorDelegate = (key) => new DelegateGenerator().Generate(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public object Eval(Expression exp)
        {
            if (exp.NodeType == ExpressionType.Constant)
            {
                return ((ConstantExpression)exp).Value;
            }

            var parameters = new ConstantExtractor().Extract(exp);
            var func = _mCache.Get(exp, _mCreatorDelegate);
            return func(parameters);
        }
    }        
}
