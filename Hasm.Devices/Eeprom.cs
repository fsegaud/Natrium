namespace Hasm.Devices
{
    // 0: index (w)
    // 1: value (rw)
    // 2: length (r)
    // 3: read_only (rw)
    public class Eeprom : IDevice
    {
        private readonly double[] _memory;
        private uint _nextIndex;
        private bool _readOnly;

        public Eeprom(int size, bool isReadOnly = false)
        {
            _memory = new double[size];
            _readOnly = isReadOnly;
        }

        public Eeprom(double[] memory, bool isReadOnly = false)
        {
            _memory = memory;
            _readOnly = isReadOnly;
        }

        public bool TryReadValue(int index, out double value)
        {
            value = 0;
            switch (index)
            {
                case 1 :
                    if (_nextIndex >= _memory.Length)
                        return false;
                    value = _memory[_nextIndex];
                    break;
            
                case 2:
                    value = _memory.Length;
                    break;
            
                case 3:
                    value = _readOnly ? 1d : 0d;
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
                    if (value < 0 || value >= _memory.Length)
                        return false;
                    _nextIndex = (uint)value;
                    break;
            
                case 1 :
                    if (_nextIndex >= _memory.Length || _readOnly)
                        return false;
                    _memory[_nextIndex] = value;
                    break;
            
                case 3:
                    _readOnly = value > 0d;
                    break;
            
                default:
                    return false;
            }

            return true;
        }
    }
}