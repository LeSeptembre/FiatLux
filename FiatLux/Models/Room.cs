namespace FiatLux.Models;

public class Room
{
    public string RoomId { get; set; }
    public double Lux { get; set; }
    public int LampPower { get; set; }
    public int TargetLux { get; set; }
    public string Mode { get; set; }
}
