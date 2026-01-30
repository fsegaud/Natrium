using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Hasm.Test;

public  class TestConfiguration
{
#pragma warning disable CS0649 // Field is assigned through json.
    public TestDescriptor[]? TestDescriptors;
#pragma warning restore CS0649

    public static TestConfiguration? Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
            
        return JsonConvert.DeserializeObject<TestConfiguration>(json);
    }
        
    public class TestDescriptor
    {
#pragma warning disable CS0649 // Field is assigned through json.
        public required string SourceFile;
        [JsonConverter(typeof(StringEnumConverter))]public required Error CompilerError;
        [JsonConverter(typeof(StringEnumConverter))]public required Error RuntimeError;
#pragma warning restore CS0649
    }
}