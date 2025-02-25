#nullable enable
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Marten.Exceptions;

namespace Marten.Services.Json.Transformations
{
    /// <summary>
    /// Defines JSON payload transformation API. This is currently used for internal, low level processing.
    /// If you want to use it explicitly, check first
    /// <a href="https://martendb.io/events/versioning.html#namespace-migration">Event Versioning documentation</a>.
    /// </summary>
    public class JsonTransformation
    {
        public Func<ISerializer, DbDataReader, int, object> FromDbDataReader { get; }
        public Func<ISerializer, DbDataReader, int, CancellationToken, ValueTask<object>> FromDbDataReaderAsync { get; }

        public JsonTransformation(
            Func<ISerializer, DbDataReader, int, object> fromDbDataReader,
            Func<ISerializer, DbDataReader, int, CancellationToken, ValueTask<object>>? transformDbDataReaderAsync =
                null
        )
        {
            FromDbDataReader = fromDbDataReader;
            FromDbDataReaderAsync =
                transformDbDataReaderAsync ?? ((serializer, reader, index, _) =>
                    new ValueTask<object>(FromDbDataReader(serializer, reader, index)));
        }
    }

    public static class JsonTransformations
    {
        public static JsonTransformation Upcast<TOldEvent, TEvent>(Func<TOldEvent, TEvent> transform)
            where TOldEvent : notnull
            where TEvent : notnull
        {
            return new JsonTransformation(FromDbDataReader(transform), FromDbDataReaderAsync(transform));
        }

        public static JsonTransformation Upcast<TOldEvent, TEvent>(
            Func<TOldEvent, CancellationToken, Task<TEvent>> transform)
            where TOldEvent : notnull
            where TEvent : notnull
        {
            return new JsonTransformation(
                (_, _, _) =>
                    throw new MartenException(
                        $"Cannot use JSON transformation of event '{typeof(TOldEvent)}' to '{typeof(TEvent)}' in the synchronous API" +
                        "It was defined as async only"
                    ),
                FromDbDataReaderAsync(transform)
            );
        }

        internal static Func<ISerializer, DbDataReader, int, object> FromDbDataReader<TOldEvent, TEvent>(
            Func<TOldEvent, TEvent> transform)
            where TOldEvent : notnull where TEvent : notnull
        {
            return (serializer, dbDataReader, index) => transform(serializer.FromJson<TOldEvent>(dbDataReader, index));
        }

        internal static Func<ISerializer, DbDataReader, int, CancellationToken, ValueTask<object>>
            FromDbDataReaderAsync<TOldEvent, TEvent>(Func<TOldEvent, CancellationToken, Task<TEvent>> transform)
            where TOldEvent : notnull where TEvent : notnull
        {
            return async (serializer, dbDataReader, index, ct) =>
                await transform(
                    await serializer.FromJsonAsync<TOldEvent>(dbDataReader, index, ct).ConfigureAwait(false),
                    ct
                ).ConfigureAwait(false);
        }

        internal static Func<ISerializer, DbDataReader, int, CancellationToken, ValueTask<object>>
            FromDbDataReaderAsync<TOldEvent, TEvent>(Func<TOldEvent, TEvent> transform)
            where TOldEvent : notnull where TEvent : notnull
        {
            return async (serializer, dbDataReader, index, ct) =>
                transform(await serializer.FromJsonAsync<TOldEvent>(dbDataReader, index, ct).ConfigureAwait(false));
        }
    }
}
