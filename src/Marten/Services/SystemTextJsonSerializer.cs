using System;
using System.Data.Common;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Marten.Services.Json;
using Marten.Util;
using Npgsql;
using Weasel.Core;

#nullable enable
namespace Marten.Services
{
    /// <summary>
    /// Serializer based on System.Text.Json
    /// </summary>
    public class SystemTextJsonSerializer: ISerializer
    {
        private EnumStorage _enumStorage = EnumStorage.AsInteger;
        private Casing _casing = Casing.Default;

        private readonly JsonSerializerOptions _clean = new();

        private readonly JsonSerializerOptions _options = new();

        private readonly JsonSerializerOptions _optionsDeserialize = new();

        private JsonDocumentOptions _optionsJsonDocumentDeserialize = new();

        private readonly JsonSerializerOptions _withTypes = new();

        public SystemTextJsonSerializer()
        {
            _optionsDeserialize.Converters.Add(new SystemObjectNewtonsoftCompatibleConverter());

            _optionsDeserialize.PropertyNamingPolicy =
                _options.PropertyNamingPolicy
                    = _clean.PropertyNamingPolicy
                        = _withTypes.PropertyNamingPolicy = null;

            _optionsDeserialize.EnableDynamicTypes();
            _options.EnableDynamicTypes();
            _clean.EnableDynamicTypes();
            _withTypes.EnableDynamicTypes();

            syncDocumentDeserializeOptions();
        }

        /// <summary>
        /// Customize the inner System.Text.Json formatter.
        /// </summary>
        /// <param name="configure"></param>
        public void Customize(Action<JsonSerializerOptions> configure)
        {
            configure(_clean);
            configure(_options);
            configure(_optionsDeserialize);
            configure(_withTypes);

            syncDocumentDeserializeOptions();
        }

        private void syncDocumentDeserializeOptions() =>
            _optionsJsonDocumentDeserialize = new JsonDocumentOptions
            {
                CommentHandling = _optionsDeserialize.ReadCommentHandling,
                MaxDepth = _optionsDeserialize.MaxDepth,
                AllowTrailingCommas = _optionsDeserialize.AllowTrailingCommas
            };

        public string ToJson(object? document)
        {
            if (document is null)
            {
                // Cannot call "GetType()" on null, so mimic Newtonsoft.Json behaviour
                return "null";
            }
            return JsonSerializer.Serialize(document, document.GetType(), _options);
        }

        public T FromJson<T>(Stream stream)
        {
            using var buffer = SharedBuffer.RentAndCopy(stream.ToSOHSkippingStream());
            return JsonSerializer.Deserialize<T>(buffer, _optionsDeserialize);
        }

        public T FromJson<T>(DbDataReader reader, int index)
        {
            return FromJson<T>(reader.GetStream(index));
        }

        public async ValueTask<T> FromJsonAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            return await JsonSerializer.DeserializeAsync<T>(stream.ToSOHSkippingStream(), _optionsDeserialize, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<T> FromJsonAsync<T>(DbDataReader reader, int index, CancellationToken cancellationToken = default)
        {
            using var stream = await reader.As<NpgsqlDataReader>().GetStreamAsync(index, cancellationToken).ConfigureAwait(false);
            return await FromJsonAsync<T>(stream, cancellationToken).ConfigureAwait(false);
        }

        public object FromJson(Type type, Stream stream)
        {
            using var buffer = SharedBuffer.RentAndCopy(stream.ToSOHSkippingStream());
            return JsonSerializer.Deserialize(buffer, type, _optionsDeserialize);
        }

        public object FromJson(Type type, DbDataReader reader, int index)
        {
            return FromJson(type, reader.As<NpgsqlDataReader>().GetStream(index));
        }

        public JsonDocument JsonDocumentFromJson(Stream stream)
        {
            using var buffer = SharedBuffer.RentAndCopy(stream.ToSOHSkippingStream());
            return JsonDocument.Parse(buffer, _optionsJsonDocumentDeserialize);
        }

        public JsonDocument JsonDocumentFromJson(DbDataReader reader, int index)
        {
            return JsonDocumentFromJson(reader.As<NpgsqlDataReader>().GetStream(index));
        }

        public async ValueTask<object> FromJsonAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
        {
            return await JsonSerializer.DeserializeAsync(stream.ToSOHSkippingStream(), type, _optionsDeserialize, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<object> FromJsonAsync(Type type, DbDataReader reader, int index, CancellationToken cancellationToken = default)
        {
            using var stream = await reader.As<NpgsqlDataReader>().GetStreamAsync(index, cancellationToken).ConfigureAwait(false);
            return await FromJsonAsync(type, stream, cancellationToken).ConfigureAwait(false);
        }

        public string ToCleanJson(object? document)
        {
            return JsonSerializer.Serialize(document, _clean);
        }

        public string ToJsonWithTypes(object document)
        {
            return JsonSerializer.Serialize(document, _withTypes);
        }

        public ValueCasting ValueCasting => ValueCasting.Strict;

        /// <inheritdoc />
        public EnumStorage EnumStorage
        {
            get => _enumStorage;
            set
            {
                _enumStorage = value;

                var jsonNamingPolicy = _casing switch
                {
                    Casing.CamelCase => JsonNamingPolicy.CamelCase,
                    Casing.SnakeCase => new JsonSnakeCaseNamingPolicy(),
                    _ => null
                };

                _optionsDeserialize.PropertyNamingPolicy =
                    _options.PropertyNamingPolicy
                        = _clean.PropertyNamingPolicy
                            = _withTypes.PropertyNamingPolicy = jsonNamingPolicy;

                _options.Converters.RemoveAll(x => x is JsonStringEnumConverter);
                _optionsDeserialize.Converters.RemoveAll(x => x is JsonStringEnumConverter);
                _clean.Converters.RemoveAll(x => x is JsonStringEnumConverter);
                _withTypes.Converters.RemoveAll(x => x is JsonStringEnumConverter);

                if (_enumStorage == EnumStorage.AsString)
                {
                    var converter = new JsonStringEnumConverter();
                    _options.Converters.Add(converter);
                    _optionsDeserialize.Converters.Add(converter);
                    _clean.Converters.Add(converter);
                    _withTypes.Converters.Add(converter);
                }

                var bigIntegerConverter = new SystemTextJsonBigIntegerConverter();
                _options.Converters.Add(bigIntegerConverter);
                _optionsDeserialize.Converters.Add(bigIntegerConverter);
                _clean.Converters.Add(bigIntegerConverter);
                _withTypes.Converters.Add(bigIntegerConverter);
            }
        }

        /// <inheritdoc />
        public Casing Casing
        {
            get => _casing;
            set
            {
                _casing = value;
                // ensure we refresh
                EnumStorage = _enumStorage;
            }
        }
    }
}
