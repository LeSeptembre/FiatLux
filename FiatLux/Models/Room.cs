namespace FiatLux.Models;

public class Sensor
{
    public string SensorId { get; set; }
    public string Type { get; set; }
    public double Value { get; set; }
    public long LastUpdate { get; set; }
    public string Status { get; set; }
}

public class Lamp
{
    public string LampId { get; set; }
    public int Power { get; set; }
    public string Status { get; set; }
    public long LastUpdate { get; set; }
}

public class Room
{
    public string RoomId { get; set; }
    public double Lux { get; set; }
    public int LampPower { get; set; }
    public int TargetLux { get; set; }
    public string Mode { get; set; }
    public bool IsManualMode { get; set; }
    public List<Sensor> Sensors { get; set; } = new();
    public List<Lamp> Lamps { get; set; } = new();
}
