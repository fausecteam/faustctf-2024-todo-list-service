namespace service.Models
{
	public class TodoItem
	{
		public int Id { get; set; }
		public long Timestamp { get; set; }
		public string Description { get; set; }
		public bool IsCompleted { get; set; }
		public string UserId { get; set; }
		public string UserName { get; set; }
		public string Category { get; set; }
	}
}