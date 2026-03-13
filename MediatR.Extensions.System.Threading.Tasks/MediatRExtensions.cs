using System.Runtime.CompilerServices;

namespace MediatR.Extensions.System.Threading.Tasks
{
    public static class MediatRExtensions
    {
        /// <summary>
        /// Sends multiple requests in parallel and returns all results.
        /// </summary>
        public static async Task<TResponse[]> SendAll<TResponse>(
            this IMediator mediator,
            IEnumerable<IRequest<TResponse>> requests,
            CancellationToken cancellationToken = default)
        {
            var tasks = requests.Select(request => mediator.Send(request, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Sends multiple requests in parallel with a maximum degree of parallelism.
        /// </summary>
        public static async Task<TResponse[]> SendAll<TResponse>(
            this IMediator mediator,
            IEnumerable<IRequest<TResponse>> requests,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken = default)
        {
            var results = new List<TResponse>();
            
            await Parallel.ForEachAsync(
                requests,
                new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken 
                },
                async (request, ct) =>
                {
                    var result = await mediator.Send(request, ct);
                    lock (results)
                    {
                        results.Add(result);
                    }
                });

            return [.. results];
        }

        /// <summary>
        /// Sends multiple requests in parallel with different response types.
        /// </summary>
        public static async Task SendAll(
            this IMediator mediator,
            IEnumerable<IRequest> requests,
            CancellationToken cancellationToken = default)
        {
            var tasks = requests.Select(request => mediator.Send(request, cancellationToken));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Sends multiple requests in parallel with a maximum degree of parallelism.
        /// </summary>
        public static async Task SendAll(
            this IMediator mediator,
            IEnumerable<IRequest> requests,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken = default)
        {
            await Parallel.ForEachAsync(
                requests,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                async (request, ct) => await mediator.Send(request, ct));
        }

        /// <summary>
        /// Sends multiple requests in parallel and returns results as they complete.
        /// </summary>
        public static async IAsyncEnumerable<TResponse> SendAsCompleted<TResponse>(
            this IMediator mediator,
            IEnumerable<IRequest<TResponse>> requests,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var tasks = requests.Select(request => mediator.Send(request, cancellationToken)).ToList();

            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                yield return await completedTask;
            }
        }

        /// <summary>
        /// Publishes multiple notifications in parallel.
        /// </summary>
        public static async Task PublishAll<TNotification>(
            this IMediator mediator,
            IEnumerable<TNotification> notifications,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            var tasks = notifications.Select(notification => mediator.Publish(notification, cancellationToken));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Publishes multiple notifications in parallel with a maximum degree of parallelism.
        /// </summary>
        public static async Task PublishAll<TNotification>(
            this IMediator mediator,
            IEnumerable<TNotification> notifications,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            await Parallel.ForEachAsync(
                notifications,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                async (notification, ct) => await mediator.Publish(notification, ct));
        }
    }
}
