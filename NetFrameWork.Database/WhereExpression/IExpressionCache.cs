using System;

namespace NetFrameWork.Database.WhereExpression
{
    /// <summary>
    /// 
    /// </summary>
    public interface IExpressionCache<T> where T : class
    {
        /// <summary>
        /// 
        /// </summary>
        T Get(System.Linq.Expressions.Expression key, Func<System.Linq.Expressions.Expression, T> creator);
    }
}
