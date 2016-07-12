namespace SwaggerMetadataRetriever
{   
    public interface IModelMetadata
    {
        string Model { get; set; }
        string Property { get; set; }
        string Type { get; set; }
        bool IsArray { get; set; }
        bool IsRequired { get; set; }
        bool IsSimpleType { get; set; }
    }
    
}
