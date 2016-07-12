using System;
using System.IO;
using System.Security.Policy;
using log4net;
using log4net.Config;

namespace SwaggerMetadataRetriever
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program).Name);

        static void Main(string[] args)
        {
            BasicConfigurator.Configure();

            try
            {
                if (args.Length == 1)
                {
                    IApiMetadataConfiguration configuration = new Configuration
                    {
                        Url = new UriBuilder(args[0]).Uri.AbsoluteUri
                    };
                    Log.Info($"Swagger URL: {configuration.Url}.");
                    var metadataRetriever = new SwaggerMetadataRetriever(configuration);
                    metadataRetriever.LoadMetadata().Wait();
                    var metadataPath = Path.Combine(configuration.Folder, "metadata.json");
                    Log.Info($"New metadata file written to {metadataPath}.");
                }
                else
                {
                    Log.Info(
                        "Single argument required: Swagger Base URL (i.e. http://apicert.doubleline.us/api/metadata)");
                }
            }
            catch (AggregateException e)
            {
                foreach (var innerException in e.InnerExceptions)
                {
                    Log.Fatal(innerException);
                }
            }
        }
    }

    internal class Configuration : IApiMetadataConfiguration
{
    public string Folder
    {
        get { return Directory.GetCurrentDirectory(); }
        set { throw new NotImplementedException(); }
    }

    public bool Force
    {
        get { return true; }
        set { throw new NotImplementedException(); }
    }

    public object Url
    {
        get; set;
    }
}
}
