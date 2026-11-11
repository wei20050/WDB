using System.Collections.Generic;
using System.Linq.Expressions;

namespace NetFrameWork.Database.WhereExpression
{
    /// <summary>
    /// 
    /// </summary>
    public class ConstantExtractor : ExpressionVisitor
    {
        private List<object> _mConstants;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public List<object> Extract(Expression exp)
        {
            _mConstants = new List<object>();
            Visit(exp);
            return _mConstants;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        protected override Expression VisitConstant(ConstantExpression c)
        {
            _mConstants.Add(c.Value);
            return c;
        }
    }        
}
