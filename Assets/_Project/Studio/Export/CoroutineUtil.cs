using System.Collections;
using System.Collections.Generic;

namespace PuzzleStudio.Studio.Export
{
    public static class CoroutineUtil
    {
        /// <summary>
        /// Expands nested IEnumerator yields into one sequence, so a caller that steps a coroutine by hand
        /// (the export loop updating its progress bar) sees every step and every exception.
        /// </summary>
        public static IEnumerator Flatten(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
                else yield return top.Current;
            }
        }
    }
}
