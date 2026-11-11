using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace NetFrameWork.Database.WhereExpression
{
    /// <summary>
    /// 
    /// </summary>
    public class DelegateGenerator : ExpressionVisitor
    {
        private static readonly MethodInfo SIndexerInfo = typeof(List<object>).GetMethod("get_Item");

        private int _mParameterCount;
        private ParameterExpression _mParametersExpression;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public Func<List<object>, object> Generate(Expression exp)
        {
            _mParameterCount = 0;
            _mParametersExpression =
                Expression.Parameter(typeof(List<object>), "parameters");

            var body = Visit(exp); // normalize
            if (body.Type != typeof(object))
            {
                body = Expression.Convert(body, typeof(object));
            }

            var lambda = Expression.Lambda<Func<List<object>, object>>(body, _mParametersExpression);
            return lambda.Compile();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        protected override Expression VisitConstant(ConstantExpression c)
        {
            Expression exp = Expression.Call(
                _mParametersExpression,
                SIndexerInfo,
                Expression.Constant(_mParameterCount++));
            return c.Type == typeof(object) ? exp : Expression.Convert(exp, c.Type);
        }
    }
}
