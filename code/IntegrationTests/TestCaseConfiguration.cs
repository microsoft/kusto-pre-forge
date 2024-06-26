﻿using Kusto.Data.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IntegrationTests
{
    internal class TestCaseConfiguration
    {
        public string Function { get; set; } = string.Empty;

        public DataSourceFormat Format { get; set; } = DataSourceFormat.psv;

        public string BlobFolder { get; set; } = string.Empty;

        public string Table { get; set; } = string.Empty;

        public bool HasHeaders { get; set; } = true;

        public DataSourceCompressionType InputCompression { get; set; }
            = DataSourceCompressionType.None;

        public DataSourceCompressionType OutputCompression { get; set; }
            = DataSourceCompressionType.None;

        public static IImmutableDictionary<string, TestCaseConfiguration> LoadConfigurations()
        {
            var text = File.ReadAllText("TestCaseConfig.json");
            var config = JsonSerializer.Deserialize<TestCaseConfiguration[]>(
                text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters =
                    {
                        new JsonStringEnumConverter()
                    }
                });

            if (config == null)
            {
                throw new InvalidDataException("Couldn't load configuration");
            }
            foreach (var c in config)
            {
                c.Validate();
                c.Clean();
            }

            return config
                .ToImmutableDictionary(c => c.Table);
        }

        public string GetCreateTableScript()
        {
            return Format == DataSourceFormat.txt
                ? $".create table {Table}(Text:string)"
                : $".set {Table} <| {Function}() | take 0";
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(Function))
            {
                throw new ArgumentNullException(nameof(Function));
            }
            if (!Enum.GetValues<DataSourceFormat>().Contains(Format))
            {
                throw new ArgumentNullException(nameof(Format));
            }
            if (string.IsNullOrWhiteSpace(BlobFolder))
            {
                throw new ArgumentNullException(nameof(BlobFolder));
            }
            if (string.IsNullOrWhiteSpace(Table))
            {
                throw new ArgumentNullException(nameof(Table));
            }
        }

        private void Clean()
        {
            BlobFolder = BlobFolder.Trim().Trim('/');
        }
    }
}