using System;
using System.Linq;
using System.Linq.Expressions;

namespace Telesale.Api.Helpers;

public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var secondBody = expr2.Body;
        var parameter = expr1.Parameters[0];
        var visitor = new ParameterReplacer(expr2.Parameters[0], parameter);
        var newBody = Expression.OrElse(expr1.Body, visitor.Visit(secondBody));
        return Expression.Lambda<Func<T, bool>>(newBody, parameter);
    }

    public static Expression<Func<T, bool>> And<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var secondBody = expr2.Body;
        var parameter = expr1.Parameters[0];
        var visitor = new ParameterReplacer(expr2.Parameters[0], parameter);
        var newBody = Expression.AndAlso(expr1.Body, visitor.Visit(secondBody));
        return Expression.Lambda<Func<T, bool>>(newBody, parameter);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _from ? _to : base.VisitParameter(node);
        }
    }
}
