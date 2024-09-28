using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using service.Models;

namespace service.Services;

public static class Serializer
{
    public static byte[] SerializeToJson<T>(T obj)
    {
        try
        {
            var jsonString = JsonConvert.SerializeObject(obj);
            var byteArray = Encoding.UTF8.GetBytes(jsonString);
            return byteArray;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return [];
    }

    public static byte[] SerializeToXML(List<TodoItem> todoItems)
    {
        var xmlSerializer = new XmlSerializer(typeof(List<TodoItem>));
        using var stringWriter = new StringWriter();
        xmlSerializer.Serialize(stringWriter, todoItems);
        var xmlString = stringWriter.ToString();
        var byteArray = Encoding.UTF8.GetBytes(xmlString);
        return byteArray;
    }

    public static List<T> DeserializeJson<T>(string requestBody)
    {
        var res = JsonConvert.DeserializeObject<dynamic>(requestBody,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects });
        if (res == null)
            return [];
        List<T> t = res.ToObject<List<T>>();
        return t;
    }

    public static List<T> DeserializeXML<T>(string requestBody)
    {
        var settings = new XmlReaderSettings
        {
            Async = false
        };
        try
        {
            using var stringReader = new StringReader(requestBody);
            using var reader = XmlReader.Create(stringReader, settings);

            var xmlSerializer = new XmlSerializer(typeof(List<T>));
            var result = xmlSerializer.Deserialize(reader) as List<T>;

            return result ?? new List<T>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deserializing XML: {ex.Message}");
            return new List<T>();
        }
    }

    public static bool IsValidXml(string xmlString)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static bool IsValidJson(string strInput)
    {
        if (string.IsNullOrWhiteSpace(strInput))
        {
            return false;
        }

        strInput = strInput.Trim();
        if ((strInput.StartsWith('{') && strInput.EndsWith('}')) || 
            (strInput.StartsWith('[') && strInput.EndsWith(']'))) 
        {
            try
            {
                var obj = JToken.Parse(strInput);
                return true;
            }
            catch (JsonReaderException jex)
            {
                Console.WriteLine(jex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
        else
        {
            return false;
        }
    }
}