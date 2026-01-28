namespace Hasm.Devices
{
    // 0: Append chat (w)
    // 1: Clear (w)
    public class Screen : IDevice
    {
        public string Display { get; private set; } = string.Empty;
    
        public bool TryReadValue(int index, out double value)
        {
            value = 0d;
            return false;
        }

        public bool TryWriteValue(int index, double value)
        {
            switch (index)
            {
                case 0 :
                    // Appends a character to the display.
                    Display += (char)value;
                    break;
            
                case 1 :
                    // Reset the display.
                    Display = string.Empty;
                    break;
            
                default:
                    return false;
            }

            return true;
        }
    }
}