using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Newtonsoft.Json;
using service.Services;

namespace service.Models;

public class Filter
{
    public int Id { get; set; }

    [NotMapped]
    private Dictionary<string, string> _query;
    private string _queryString;
    public string User { get; set; }
    public string Name { get; set; }
    
    public string QueryString {
        get => _queryString;
        init
        {
            _queryString = value;
            if (_query == null)
                _query = JsonConvert.DeserializeObject<Dictionary<string, string>>(QueryString);
            if (Name != null && User != null)
                FilterManager.AddFilter(this);
        }
    }
    
    [NotMapped]
    public Dictionary<string, string> Query
    {
        get
        {
            if (_query == null && _queryString != null) 
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(QueryString);
            return _query;
        }
        set
        {
            _query = value;
            if (QueryString == null)
                _queryString = Encoding.UTF8.GetString(Serializer.SerializeToJson<Dictionary<string, string>>(value));
            if (Name != null && User != null)
                FilterManager.AddFilter(this);
            
        }
    }
}