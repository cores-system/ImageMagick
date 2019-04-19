using System;
using System.Text;

namespace ImageMagick.Engine
{
    public class Base64
    {
        public static string Decode(string base64Encoded)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64Encoded));
        }
    }
}
