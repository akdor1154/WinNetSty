using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace WinNetSty {
    static class PointExtension {
        public static Vector2 ToVector2(this Point point) {
            return new Vector2((float)point.X, (float)point.Y);
        }
    }

    static class ColorExtension {
        public static Color withAlpha(this Color color, byte alpha) {
            Color newColor = color;
            newColor.A = alpha;
            return newColor;
        }
        public static Color withAlpha(this Color color, float alpha) {
            return color.withAlpha((byte) (Byte.MaxValue * alpha));
        }
    }

    static class ushortExtension {
        public static void setFromUshort(this byte[] array, int index, ushort number) {
            byte[] bytesToCopy = System.BitConverter.GetBytes(IPAddress.HostToNetworkOrder(number));
            Array.Copy(bytesToCopy, 2, array, index, 2);
        }
    }
}
