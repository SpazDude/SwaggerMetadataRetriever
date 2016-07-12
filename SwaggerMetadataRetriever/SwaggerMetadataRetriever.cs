using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using log4net;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable CollectionNeverUpdated.Local
// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable AccessToDisposedClosure

namespace SwaggerMetadataRetriever
{
    public class SwaggerMetadataRetriever
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SwaggerMetadataRetriever).Name);

        private class Metadata
        {
            public string value { get; set; }
            public string sectionPrefix { get; set; }
        }

        private class ApiDoc
        {
            public class Api { public string path { get; set; } }
            public Api[] apis { get; set; }
        }

        private class Resource
        {
            public class Items
            {
                [JsonProperty(PropertyName = "ref")]
                public string reference { get; set; }
            }

            public class Property
            {
                public string type { get; set; }
                public bool required { get; set; }
                public Items items { get; set; }
                public string description { get; set; }
            }

            public class Model
            {
                public Dictionary<string, Property> properties { get; set; }
            }

            public Dictionary<string, Model> models { get; set; }
        }

        private readonly IApiMetadataConfiguration _configuration;

        public SwaggerMetadataRetriever(IApiMetadataConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IEnumerable<JsonModelMetadata>> GetMetadata()
        {
            if (!MetadataExists || _configuration.Force) await LoadMetadata();
            return await ReadMetadata();
        }

        private string Filename => Path.Combine(_configuration.Folder, "metadata.json");

        public bool MetadataExists => File.Exists(Filename);

        public async Task<IEnumerable<JsonModelMetadata>> ReadMetadata()
        {
            var result = new List<JsonModelMetadata>();
            if (!MetadataExists) return result;
            using (var reader = new StreamReader(Filename))
            {
                while (!reader.EndOfStream)
                {
                    var obj = await reader.ReadLineAsync();
                    var info = JsonConvert.DeserializeObject<JsonModelMetadata>(obj);
                    result.Add(info);
                }
                reader.Close();
            }
            return result;
        }

        public async Task LoadMetadata()
        {
            File.Delete(Filename);
            Log.Info("Loading API Metadata");
            using (var writer = new StreamWriter(Filename))
            {
                var metadataBlock = new BufferBlock<string>();
                var apidocsBlock = new TransformManyBlock<string, string>(async x =>
                {
                    var j = await LoadJsonString(x);
                    var docs = JsonConvert.DeserializeObject<ApiDoc>(j);
                    return docs.apis.Select(y => $"{x}{y.path}");
                });
                var resourcesBlock = new TransformManyBlock<string, JsonModelMetadata>(async x =>
                {
                    var j = await LoadJsonString(x);
                    j = j.Replace("\"$ref\"", "\"ref\"");
                    var parts = x.Split(Path.AltDirectorySeparatorChar);
                    var resources = JsonConvert.DeserializeObject<Resource>(j, new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.Objects });
                    var result = (from model in resources.models
                                  from property in model.Value.properties
                                  select new JsonModelMetadata
                                  {
                                      Category = parts[0],
                                      Resource = parts[2],
                                      Model = model.Key,
                                      Property = property.Key,
                                      Type = property.Value.type == "array" ? property.Value.items.reference : property.Value.type,
                                      IsArray = property.Value.type == "array",
                                      IsRequired = property.Value.required,
                                      Description = property.Value.description
                                  }).ToList();

                    return result;
                });
                var outputBlock = new ActionBlock<JsonModelMetadata>(async x =>
                {
                    var str = JsonConvert.SerializeObject(x, Formatting.None);
                    await writer.WriteLineAsync(str);
                });

                //link blocks
                metadataBlock.LinkTo(apidocsBlock, new DataflowLinkOptions { PropagateCompletion = true });
                apidocsBlock.LinkTo(resourcesBlock, new DataflowLinkOptions { PropagateCompletion = true });
                resourcesBlock.LinkTo(outputBlock, new DataflowLinkOptions { PropagateCompletion = true });

                //prime the pipeline
                var json = await LoadJsonString("");
                var metadata = JsonConvert.DeserializeObject<Metadata[]>(json);
                foreach (var mUrl in metadata.Where(m => m.sectionPrefix == null).Select(m => $"{m.value}/api-docs"))
                {
                    metadataBlock.Post(mUrl);
                }
                metadataBlock.Complete();

                await outputBlock.Completion;
                writer.Close();
            }
        }

        private async Task<string> LoadJsonString(string localUrl)
        {
            using (var client = new HttpClient { Timeout = new TimeSpan(0, 0, 5, 0) })
            {
                var response = await client.GetAsync($"{_configuration.Url}/{localUrl}");
                response.EnsureSuccessStatusCode();
                Log.Info($"loaded {localUrl}");
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
