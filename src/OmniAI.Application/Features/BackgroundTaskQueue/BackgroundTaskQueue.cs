using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace OmniAI.Application.Features.BackgroundTaskQueue
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

        public async Task EnqueueAsync(Guid knowledgeSourceId, CancellationToken ct = default)
        {
            await _queue.Writer.WriteAsync(knowledgeSourceId, ct);
        }

        public async Task<Guid> DequeueAsync(CancellationToken ct)
        {
            return await _queue.Reader.ReadAsync(ct);
        }
    }
}
