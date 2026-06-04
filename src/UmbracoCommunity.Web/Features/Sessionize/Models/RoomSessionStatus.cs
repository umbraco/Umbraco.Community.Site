namespace UmbracoCommunity.Web.Features.Sessionize.Models;

public class RoomSessionStatus
{
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public SessionizeSession? Current { get; set; }
    public SessionizeSession? Next { get; set; }
}
