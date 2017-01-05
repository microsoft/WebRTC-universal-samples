using System.Threading.Tasks;
using Windows.Foundation;

namespace ChatterBox.Server.Helpers
{
    public static class WindowsRuntimeSystemExtensions
    {
        /// <summary>
        ///     Returns a Windows Runtime asynchronous action that represents a started task.
        /// </summary>
        /// <returns>
        ///     A Windows.Foundation.IAsyncAction instance that represents the started task.
        /// </returns>
        /// <param name="source">The started task. </param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null. </exception>
        /// <exception cref="T:System.InvalidOperationException"><paramref name="source" /> is an unstarted task. </exception>
        public static IAsyncAction CastToAsyncAction(this Task source)
        {
            return null;
        }

        /// <summary>
        ///     Returns a task that represents a Windows Runtime asynchronous action.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous action.
        /// </returns>
        /// <param name="source">The asynchronous action. </param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="source" /> is null. </exception>
        public static Task CastToTask(this IAsyncAction source)
        {
            return Task.CompletedTask;
        }
    }
}