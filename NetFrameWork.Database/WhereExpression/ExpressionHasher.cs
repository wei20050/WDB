using System.Linq.Expressions;

namespace NetFrameWork.Database.WhereExpression
{
    /// <summary>
    /// 
    /// </summary>
    public class ExpressionHasher : ExpressionVisitor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        public int Hash(Expression exp)
        { 
            HashCode = 0;
            Visit(exp);
            return HashCode;
        }

        /// <summary>
        /// 
        /// </summary>
        public int HashCode { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual ExpressionHasher Hash(int value)
        {
            unchecked { HashCode += value; }
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual ExpressionHasher Hash(bool value)
        {
            unchecked { HashCode += value ? 1 : 0; }
            return this;
        }

        private static readonly object SNullValue = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual ExpressionHasher Hash(object value)
        {
            value ??= SNullValue;
            unchecked { HashCode += value.GetHashCode(); }
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        protected override Expression Visit(Expression exp)
        {
            if (exp == null) return null;

            Hash((int)exp.NodeType).Hash(exp.Type);
            return base.Visit(exp);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        protected override Expression VisitBinary(BinaryExpression b)
        {
            Hash(b.IsLifted).Hash(b.IsLiftedToNull).Hash(b.Method);
            return base.VisitBinary(b);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binding"></param>
        /// <returns></returns>
        protected override MemberBinding VisitBinding(MemberBinding binding)
        {
            Hash(binding.BindingType).Hash(binding.Member);
            return base.VisitBinding(binding);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        protected override Expression VisitConstant(ConstantExpression c)
        {
            Hash(c.Value);
            return base.VisitConstant(c);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="initializer"></param>
        /// <returns></returns>
        protected override ElementInit VisitElementInitializer(ElementInit initializer)
        {
            Hash(initializer.AddMethod);
            return base.VisitElementInitializer(initializer);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override Expression VisitLambda(LambdaExpression lambda)
        {
            foreach (var p in lambda.Parameters)
            {
                VisitParameter(p);
            }

            return base.VisitLambda(lambda);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            Hash(m.Member);
            return base.VisitMemberAccess(m);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            Hash(m.Method);
            return base.VisitMethodCall(m);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override NewExpression VisitNew(NewExpression nex)
        {
            Hash(nex.Constructor);
            if (nex.Members != null)
            {
                foreach (var m in nex.Members) Hash(m);
            }

            return base.VisitNew(nex);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override Expression VisitParameter(ParameterExpression p)
        {
            Hash(p.Name);
            return base.VisitParameter(p);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override Expression VisitTypeIs(TypeBinaryExpression b)
        {
            Hash(b.TypeOperand);
            return base.VisitTypeIs(b);
        }
        /// <summary>
        /// 
        /// </summary>
        protected override Expression VisitUnary(UnaryExpression u)
        {
            Hash(u.IsLifted).Hash(u.IsLiftedToNull).Hash(u.Method);
            return base.VisitUnary(u);
        }
    }
}
