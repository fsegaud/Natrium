namespace GameTest;

// 0 -> Index (W)
// 1 -> Value (W)
// 2 -> Clear (W)
// 3 -> Width (R)
// 4 -> Height (R)
public class VirtualScreen(int width, int height) : Natrium.IDevice
{
    private uint _nextIndex;
    
    public char[] Data { get; } = new char[width * height];
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;
    
    public bool TryReadValue(int index, out double value)
    {
        value = 0;
        switch (index)
        {
            case 1 :
                if (_nextIndex >= Data.Length)
                    return false;
                value = Data[_nextIndex];
                break;
            
            case 3 :
                value = Width;
                break;
            
            case 4 :
                value = Height;
                break;
            
            default:
                return false;
        }

        return true;
    }

    public bool TryWriteValue(int index, double value)
    {
        switch (index)
        {
            case 0 :
                if (value < 0 || value >= Data.Length)
                    return false;
                _nextIndex = (uint)value;
                break;
            
            case 1 :
                if (_nextIndex >= Data.Length)
                    return false;
                Data[_nextIndex] = (char)value;
                break;
            
            case 2 :
                Array.Fill(Data, ' ');
                break;

            default:
                return false;
        }

        return true;
    }
}
