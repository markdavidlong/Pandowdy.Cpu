using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services
{
    ////////////////////////////////////////////////////////////////////////////////////

    public sealed class FrameProvider : IFrameProvider
    {
        private const int W = 80;
        private const int H = 192;
        private BitmapDataArray _front = new();
        private BitmapDataArray _back = new();
        public int Width => W;
        public int Height => H;
        public event EventHandler? FrameAvailable;
        public bool IsGraphics { get; set; } = false;
        public bool IsMixed { get; set; } = false;
        public BitmapDataArray GetFrame() => _front;
        public BitmapDataArray BorrowWritable() => _back;
        public void CommitWritable()
        {
            (_back, _front) = (_front, _back);
            FrameAvailable?.Invoke(this, EventArgs.Empty);
        }
    }
}
