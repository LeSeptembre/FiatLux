namespace FiatLux.Models;

public class Sensor : BindableObject
{
    public string SensorId { get; set; }
    public string Type { get; set; }
    
    private double _value;
    public double Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }
    
    private long _lastUpdate;
    public long LastUpdate
    {
        get => _lastUpdate;
        set { _lastUpdate = value; OnPropertyChanged(); }
    }
    
    private string _status;
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }
}

public class Lamp : BindableObject
{
    public string LampId { get; set; }
    
    private int _power;
    public int Power
    {
        get => _power;
        set { _power = value; OnPropertyChanged(); }
    }
    
    private string _status;
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }
    
    private long _lastUpdate;
    public long LastUpdate
    {
        get => _lastUpdate;
        set { _lastUpdate = value; OnPropertyChanged(); }
    }
}

public class Room : BindableObject
{
    public string RoomId { get; set; }
    
    private double _lux;
    public double Lux
    {
        get => _lux;
        set { _lux = value; OnPropertyChanged(); }
    }
    
    private int _lampPower;
    public int LampPower
    {
        get => _lampPower;
        set { _lampPower = value; OnPropertyChanged(); }
    }
    
    private int _targetLux;
    public int TargetLux
    {
        get => _targetLux;
        set { _targetLux = value; OnPropertyChanged(); }
    }
    
    private string _mode;
    public string Mode
    {
        get => _mode;
        set { _mode = value; OnPropertyChanged(); }
    }
    
    private bool _isManualMode;
    public bool IsManualMode
    {
        get => _isManualMode;
        set { _isManualMode = value; OnPropertyChanged(); }
    }
    
    public List<Sensor> Sensors { get; set; } = new();
    public List<Lamp> Lamps { get; set; } = new();
}
