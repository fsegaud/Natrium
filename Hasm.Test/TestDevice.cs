namespace Hasm.Test;

public class TestDevice : IDevice
{
    private double _value;
        
    public bool TryReadValue(int index, out double value)
    {
        value = _value;
        return index > 0;
    }

    public bool TryWriteValue(int index, double value)
    {
        _value = index * value;
        return index > 0;
    }
}