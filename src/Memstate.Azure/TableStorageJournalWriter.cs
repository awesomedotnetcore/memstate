using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Memstate.Configuration;
using Streamstone;

namespace Memstate.Azure
{
    public class TableStorageJournalWriter : BatchingJournalWriter
    {
        private Stream _head;

        private readonly ISerializer _serializer;
        
        public TableStorageJournalWriter(Stream stream)
        {
            _serializer = Config.Current.CreateSerializer();
            _head = stream;
        }

        protected override async Task OnCommandBatch(IEnumerable<Command> commands)
        {
            var events = ToEventData(commands).ToArray();
            
            while (true)
            {
                try
                {
                    var result = await Stream.WriteAsync(_head, events);
                    _head = result.Stream;
                    break;

                }
                catch (ConcurrencyConflictException cce)
                {
                    _head = await Stream.OpenAsync(cce.Partition);
                }
            }
        }

        private IEnumerable<EventData> ToEventData(IEnumerable<Command> commands)
        {
            foreach (var command in commands)
            {
                var properties = new
                {
                    Id = command.CommandId,
                    Type = command.GetType().Name,
                    Command = _serializer.ToString(command),
                    Written = new DateTimeOffset()
                };

                var eventId = EventId.From(command.CommandId);
                yield return new EventData(eventId, EventProperties.From(properties));
            }
        }
    }
}