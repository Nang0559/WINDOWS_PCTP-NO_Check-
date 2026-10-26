using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Threading.Tasks;

namespace PCTP.Shared.UiMd
{
    public class WorkflowEngine
    {
        private readonly IWorkflowRepository _repo;
        public WorkflowEngine(IWorkflowRepository repo) => _repo = repo;

        public bool IsValidTransition(string processCode, int from, int to)
        {
            if (from == to) return true;
            return _repo.GetTransitions(processCode)
                .Any(t => t.FromStatus == from && t.ToStatus == to);
        }

        public WorkflowTransition ResolveNext<T>(string processCode, int currentStatus, T ctx)
        {
            var candidates = _repo.GetTransitions(processCode)
                .Where(t => t.FromStatus == currentStatus).ToList();

            if (candidates.Count == 0) return null; // trạng thái cuối

            // ưu tiên transition không có điều kiện (Description rỗng) là default
            var withCondition = candidates.Where(t => !string.IsNullOrWhiteSpace(t.Description));
            foreach (var t in withCondition)
                if (EvaluateCondition(t.Description, ctx)) return t;

            return candidates.FirstOrDefault(t => string.IsNullOrWhiteSpace(t.Description));
        }

        private bool EvaluateCondition<T>(string rule, T dataContext)
        {
            try
            {
                if (dataContext == null) return false;

                string expression = rule.Trim();

                // Rút gọn cú pháp nếu cột Description trong CSDL ghi ngắn dạng: "= 0", "> 0", "== 25"
                if (expression.StartsWith("=") || expression.StartsWith(">") || expression.StartsWith("<") || expression.StartsWith("!"))
                {
                    if (expression.StartsWith("="))
                        expression = "== " + expression.Substring(1);

                    expression = "it " + expression;
                }

                var p = System.Linq.Expressions.Expression.Parameter(typeof(T), "it");
                var e = DynamicExpressionParser.ParseLambda(new[] { p }, typeof(bool), expression);
                return (bool)e.Compile().DynamicInvoke(dataContext);
            }
            catch
            {
                // Nếu Description chỉ là ghi chú chữ thường (ví dụ: "Chuyển phiếu..."), không phải biểu thức code -> Bỏ qua coi như không thỏa mãn
                return false;
            }
        }
    
    }
}
