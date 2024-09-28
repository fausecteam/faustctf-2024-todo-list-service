using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using service.Data;
using service.Models;
using service.Services;

namespace service.Controllers;

[Authorize]
public class TodoController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TodoController> _logger;
    private int _itemId;

    public TodoController(ApplicationDbContext context, ILogger<TodoController> logger)
    {
        _logger = logger;
        _context = context;
        if (!_context.TodoItems.Any())
        {
            _itemId = 0;
        }
        else
        {
            _itemId = _context.TodoItems.Max(t => t.Id) + 1;
        }
    }

    [HttpGet]
    public IActionResult ListTodos()
    {
        _logger.LogInformation("GET ListTodos");

        ViewBag.Filters = GetFilters();
        ViewBag.TodoItems = GetTodoItems();
        return View();
    }

    private List<Filter> GetFilters()
    {
        var filters = _context.Filters
            .Where(filter => filter.User == User.Identity.Name)
            .ToList();
        return filters;
    }

    private List<TodoItem> GetTodoItems()
    {
        List<TodoItem> todoItems = _context.TodoItems
            .Where(todo => todo.UserName == User.Identity.Name).ToList();
        return todoItems;
    }

    [HttpGet]
    public IActionResult AddTodo()
    {
        _logger.LogInformation("GET AddTodo");
        return View();
    }

    [HttpPost]
    public IActionResult AddTodo(TodoItem item)
    {
        ViewBag.Filters = GetFilters();
        ViewBag.TodoItems = GetTodoItems();
        if (item.Description.Length >= 1024)
        {
            ViewBag.Message = "Description must be less than or equal to 1024 characters";
            Response.StatusCode = 400;
            return View("ListTodos");
        }

        _logger.LogInformation("Adding item: " + item.Description);
        item.Id = _itemId++;
        item.UserId = UserManager.GetUserId(User.Identity.Name);
        item.IsCompleted = false;
        item.UserName = User.Identity.Name;
        if (item.Category == null)
        {
            item.Category = "";
        }

        DateTimeOffset now = DateTimeOffset.Now; // TODO get times right
        long unixTimestamp = now.ToUnixTimeSeconds();
        item.Timestamp = unixTimestamp;
        _context.TodoItems.Add(item);
        _context.SaveChanges();
        return View("ListTodos");
    }

    [HttpPost]
    public IActionResult UpdateCheckboxState(int id, bool isChecked)
    {
        try
        {
            var item = _context.TodoItems.Single(item => item.Id == id && item.UserName == User.Identity.Name);
            item.IsCompleted = isChecked;
            _logger.LogInformation("Updated state of ID " + item.Id);
            _context.SaveChanges();
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("ID " + id + " not found");
            return NotFound();
        }

        return Ok();
    }

    [HttpGet]
    public ActionResult Export(string format)
    {
        _logger.LogInformation("GET Export");
        List<TodoItem> todoItems = _context.TodoItems
            .Where(todo => todo.UserName == User.Identity.Name).ToList();
        FileStreamResult stream;
        switch (format)
        {
            case "json":
                stream = File(new MemoryStream(Serializer.SerializeToJson(todoItems)), "application/json",
                    "todos.json");
                break;
            case "xml":
                stream = File(new MemoryStream(Serializer.SerializeToXML(todoItems)), "application/xml",
                    "todos.xml");
                break;
            default: return BadRequest("Missing format");
        }

        return stream;
    }

    [HttpPost]
    public IActionResult Import(IFormFile file)
    {
        _logger.LogInformation("POST Import");
        try
        {
            string requestBody = "";
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                requestBody = reader.ReadToEnd();
            }

            List<TodoItem> result = [];
            if (Serializer.IsValidXml(requestBody))
            {
                result = Serializer.DeserializeXML<TodoItem>(requestBody);
            }
            else if (Serializer.IsValidJson(requestBody))
            {
                result = Serializer.DeserializeJson<TodoItem>(requestBody);
            }

            foreach (var todoItem in result)
            {
                AddTodo(todoItem);
            }
        }
        catch (Exception ex)
        {
            ViewBag.Message = "Import failed: " + ex.Message; ;
            Response.StatusCode = 400;
        }
        ViewBag.Filters = GetFilters();
        ViewBag.TodoItems = GetTodoItems();
        return View("ListTodos");
    }

    [HttpGet]
    public IActionResult ApplyFilter(string name)
    {
        var applyFilter = _context.Filters.FirstOrDefault(f => f.Name == name && f.User == User.Identity.Name);
        if (applyFilter == null)
        {
            ViewBag.Filters = GetFilters();
            ViewBag.TodoItems = GetTodoItems();
            ViewBag.Message = "Filter not found";
            Response.StatusCode = 400;
            return View("ListTodos");
        }

        try
        {
            var query = _context.TodoItems.AsQueryable(); // Start with the base query
            if (!string.IsNullOrEmpty(applyFilter.Query["Category"]))
            {
                query = query.Where(ti => ti.Category == applyFilter.Query["Category"]);
            }

            if (long.TryParse(applyFilter.Query["FromTime"], out long fromTime) && fromTime != -1)
            {
                query = query.Where(ti => ti.Timestamp >= fromTime);
            }

            if (long.TryParse(applyFilter.Query["ToTime"], out long toTime) && toTime != -1)
            {
                query = query.Where(ti => ti.Timestamp <= toTime);
            }

            query = query.Where(ti => applyFilter.Query["User"] == ti.UserName);
            var filteredTodoItems = query.ToList();
            ViewBag.Filters = GetFilters();
            ViewBag.TodoItems = filteredTodoItems;
            return View("ListTodos");
        }
        catch (Exception ex)
        {
            if (ex is NullReferenceException or InvalidOperationException)
            {
                return NotFound();
            }

            ViewBag.Message = "Error applying filter";
            Console.WriteLine(ex.Message);
            ViewBag.Filters = GetFilters();
            ViewBag.TodoItems = GetTodoItems();
            return View("ListTodos");
        }
    }

    [HttpPost]
    public IActionResult AddFilter(string name, string category, DateTime? fromDate, TimeSpan? fromTime,
        DateTime? toDate, TimeSpan? toTime)
    {
        _logger.LogInformation("POST AddFilter");
        ViewBag.Filters = GetFilters();
        ViewBag.TodoItems = GetTodoItems();
        if (FilterManager.FilterExists(name, User.Identity.Name))
        {
            ViewBag.Message = "Filter " + name + " already exists";
            Response.StatusCode = 409;
            return View("ListTodos");
        }

        DateTime? fullFromDateTime = null;
        if (fromDate.HasValue && fromTime.HasValue)
        {
            fullFromDateTime = fromDate.Value.Add(fromTime.Value);
        }

        long unixFromTime = fullFromDateTime.HasValue
            ? ((DateTimeOffset)fullFromDateTime.Value).ToUniversalTime().ToUnixTimeSeconds()
            : -1;

        DateTime? fullToDateTime = null;
        if (toDate.HasValue && toTime.HasValue)
        {
            fullToDateTime = toDate.Value.Add(toTime.Value);
        }

        long unixToTime = fullToDateTime.HasValue
            ? ((DateTimeOffset)fullToDateTime.Value).ToUniversalTime().ToUnixTimeSeconds()
            : -1;
        var filter = new Filter();
        var query = new Dictionary<string, string>();
        query["Category"] = category == null ? "" : category;
        query["FromTime"] = unixFromTime.ToString();
        query["ToTime"] = unixToTime.ToString();
        query["User"] = User.Identity.Name;
        filter.User = User.Identity.Name;
        filter.Name = name;
        filter.Query = query;
        FilterManager.AddFilter(filter);
        return View("ListTodos");
    }
}