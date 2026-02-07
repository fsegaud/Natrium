namespace Natrium.Devices
{
    // 0 -> Index (W)
    // 1 -> Value (W)
    // 2 -> Color (W)
    // 3 -> Clear (W)
    // 4 -> AutoIncrement (W)
    // 5 -> Width (R)
    // 6 -> Height (R)
    // Colors: 0: Default, 1: Red, 2: Green, 3: Blue, 4: Cyan, 5:Magenta, 6: Yellow
    //         A: Black, B: White, C: Gray, D: DarkGray
    public class Screen : IDevice
    {
        private uint _nextIndex;

        public Screen(int width, int height)
        {
            Data = new char[width * height];
            Color = new byte[width * height];
            Width = width;
            Height = height;
        }

        public char[] Data { get; }
        public byte[] Color { get; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private bool _autoIncrement;

        public bool TryReadValue(int index, out double value)
        {
            value = 0;
            switch (index)
            {
                case 1:
                    if (_nextIndex >= Data.Length)
                        return false;
                    value = Data[_nextIndex];
                    break;

                case 5:
                    value = Width;
                    break;

                case 6:
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
                case 0:
                    if (value < 0 || value >= Data.Length)
                        return false;
                    _nextIndex = (uint)value;
                    break;

                case 1:
                    if (_nextIndex >= Data.Length)
                        return false;
                    Data[_nextIndex] = (char)value;
                    if (_autoIncrement)
                    {
                        // if auto increment, apply color to next cell.
                        if (_nextIndex + 1 < Color.Length)
                            Color[_nextIndex + 1] = Color[_nextIndex];
                        _nextIndex++;
                    }

                    break;
                
                case 2:
                    if (_nextIndex >= Color.Length)
                        return false;
                    Color[_nextIndex] = (byte)value;
                    break;

                case 3:
                    System.Array.Fill(Data, ' ');
                    System.Array.Fill(Color, (byte)0);
                    break;
                
                case 4 :
                    _autoIncrement = value > 0;
                    break;

                default:
                    return false;
            }

            return true;
        }
    }
}