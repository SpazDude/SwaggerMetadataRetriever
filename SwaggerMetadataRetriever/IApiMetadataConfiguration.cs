namespace SwaggerMetadataRetriever
{
    public interface IApiMetadataConfiguration
    {
        string Folder { get; set; }
        bool Force { get; set; }
        object Url { get; set; }
    }
}